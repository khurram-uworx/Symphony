using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Symphony.Agent;
using Symphony.Config;
using Symphony.Data;
using Symphony.Domain;
using Symphony.IssueTracker;
using Symphony.Security;
using Symphony.Orchestration;
using Symphony.Services;
using Symphony.Workflows;
using Symphony.Workspaces;
using Symphony.Utils;

namespace Symphony.Processing;

public sealed class JobProcessor : BackgroundService
{
    readonly IServiceScopeFactory scopeFactory;
    readonly IDbContextFactory<SymphonyDbContext> dbFactory;
    readonly IJobQueue jobQueue;
    readonly ILogger<JobProcessor> logger;
    const int GlobalConcurrency = 4;
    readonly ConcurrentDictionary<Guid, CancellationTokenSource> running = new();

    public JobProcessor(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<SymphonyDbContext> dbFactory,
        IJobQueue jobQueue,
        ILogger<JobProcessor> logger)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        this.jobQueue = jobQueue ?? throw new ArgumentNullException(nameof(jobQueue));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool Cancel(Guid jobId)
    {
        if (running.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RecoverStaleRunningJobsAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var cts in running.Values)
            cts.Cancel();

        await base.StopAsync(cancellationToken);

        foreach (var cts in running.Values)
            cts.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAvailableQueuedJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job polling tick failed.");
            }

            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            waitCts.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                if (jobQueue is JobQueue concreteQueue)
                    await concreteQueue.WaitForSignalInstanceAsync(waitCts.Token);
                else
                    await Task.Delay(TimeSpan.FromSeconds(5), waitCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Safety poll timeout; loop and re-check database.
            }
        }

    }

    async Task RecoverStaleRunningJobsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var staleJobs = await db.Jobs
            .Where(job => job.State == "Running")
            .ToListAsync(cancellationToken);

        foreach (var job in staleJobs)
        {
            job.State = "Failed";
            job.ErrorCode = "interrupted";
            job.ErrorMessage = "Job was interrupted by a restart.";
            job.CompletedAt = DateTimeOffset.UtcNow;
            db.JobEvents.Add(new JobEvent
            {
                JobId = job.Id,
                Timestamp = DateTimeOffset.UtcNow,
                EventName = "job.interrupted",
                Summary = "Job was interrupted by a restart."
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    async Task ProcessAvailableQueuedJobsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && running.Count < GlobalConcurrency)
        {
            var claimed = await TryClaimQueuedJobAsync(stoppingToken);
            if (claimed is null)
                return;

            var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            if (!running.TryAdd(claimed.Id, jobCts))
            {
                jobCts.Dispose();
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await RunClaimedJobAsync(claimed, jobCts, stoppingToken);
                }
                finally
                {
                    running.TryRemove(claimed.Id, out _);
                    jobCts.Dispose();
                }
            }, stoppingToken);
        }
    }

    async Task<Job?> TryClaimQueuedJobAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.Jobs
            .Where(candidate => candidate.State == "Queued")
            .OrderBy(candidate => candidate.QueuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
            return null;

        job.State = "Running";
        job.StartedAt = DateTimeOffset.UtcNow;
        job.ErrorCode = null;
        job.ErrorMessage = null;
        await db.SaveChangesAsync(cancellationToken);

        var snapshotId = job.Id;
        await using var verifyContext = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await verifyContext.Jobs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);
    }

    async Task RunClaimedJobAsync(Job job, CancellationTokenSource jobCts, CancellationToken hostToken)
    {
        try
        {
            await ProcessJobAsync(job, jobCts.Token);
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            await MarkJobStateAsync(job.Id, "Cancelled", null, "Job cancelled.", null);
            await AppendEventAsync(job.Id, null, "job.cancelled", "Job cancelled.", null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed.", job.Id);
            await MarkJobStateAsync(job.Id, "Failed", "job_error", Trim(ex.Message), null);
            await AppendEventAsync(job.Id, null, "job.failed", Trim(ex.Message), null, CancellationToken.None);
        }
    }

    async Task ProcessJobAsync(Job job, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var secretProtector = sp.GetRequiredService<ISecretProtector>();
        var configurationService = sp.GetRequiredService<ConfigurationService>();
        var workflowService = sp.GetRequiredService<WorkflowService>();
        var shell = sp.GetService<ShellCommandRunner>() ?? new ShellCommandRunner(sp.GetRequiredService<ILogger<ShellCommandRunner>>());
        var processorLogger = sp.GetRequiredService<ILogger<JobProcessor>>();

        await WorkflowService.EnsureDefaultWorkflowAsync(dbFactory, job.OwnerUserId, cancellationToken);
        var userConfig = await configurationService.GetAsync(job.OwnerUserId, cancellationToken);
        if (userConfig is null)
            throw new InvalidOperationException("Owner configuration not found.");

        var workflow = await workflowService.GetAsync(job.OwnerUserId, job.WorkflowName, cancellationToken)
            ?? await workflowService.GetAsync(job.OwnerUserId, WorkflowService.DefaultWorkflowName, cancellationToken);
        if (workflow is null)
            throw new InvalidOperationException("Workflow not found.");

        var config = ServiceConfigFactory.Build(userConfig, workflow.Content);
        var hasTickets = TryResolveTicketIds(job, secretProtector, out var ticketIds);
        IIssueTracker tracker = hasTickets
            ? IssueTrackerFactory.Create(config)
            : new NullIssueTracker();

        var snapshot = TryParseSnapshot(job, secretProtector);
        var workItems = ReadWorkItems(snapshot);
        var issues = new List<Issue>();

        if (hasTickets)
        {
            var fetched = new List<Issue>();
            try
            {
                fetched = (await tracker.FetchIssueStatesByIdsAsync(ticketIds, cancellationToken)).ToList();
            }
            catch (Exception ex)
            {
                processorLogger.LogWarning(ex, "Failed to resolve ticket IDs.");
            }

            var foundIds = new HashSet<string>(fetched.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var ticketId in ticketIds)
            {
                var issue = fetched.FirstOrDefault(item =>
                    string.Equals(item.Id, ticketId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Identifier, ticketId, StringComparison.OrdinalIgnoreCase));
                if (issue is not null)
                {
                    issues.Add(issue);
                }
                else
                {
                    await CreateFailedIssueRowAsync(job.Id, ticketId, ticketId, "Ticket not found.", cancellationToken);
                }
            }
        }

        foreach (var workItem in workItems)
            issues.Add(CreateIssueFromWorkItem(workItem));

        foreach (var issueState in issues.Select(item => item.State).Distinct(StringComparer.OrdinalIgnoreCase))
            config.ActiveStates.Add(issueState);

        issues = issues
            .OrderBy(issue => issue.Priority ?? int.MaxValue)
            .ThenBy(issue => issue.CreatedAt ?? DateTimeOffset.MaxValue)
            .ToList();

        await AppendEventAsync(job.Id, null, "job.started", "Job started.", null, cancellationToken);

        var allSuccess = true;
        var anyFailure = false;
        var baseRoot = config.Workspace.Root;
        var scopedConfig = config with { Workspace = new WorkspaceConfig(Path.Combine(baseRoot, job.OwnerUserId)) };
        var configProvider = new StaticServiceConfigProvider(scopedConfig);
        var workspaceManager = new WorkspaceManager(configProvider, shell, processorLogger is ILogger<WorkspaceManager> wmLogger ? wmLogger : Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceManager>.Instance);
        var codingAgent = TryCreateCodingAgent(scopedConfig, processorLogger);

        foreach (var issue in issues)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            await AppendEventAsync(job.Id, null, "issue.started", $"Started {issue.Identifier}.", null, cancellationToken);
            var issueRowId = await CreateIssueRowAsync(job.Id, issue, scopedConfig.Workspace.Root, cancellationToken);
            var runner = new AgentRunner(
                configProvider,
                workspaceManager,
                codingAgent,
                tracker,
                processorLogger is ILogger<AgentRunner> arLogger ? arLogger : Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentRunner>.Instance);

            var runResult = await RunIssueWithRetryAsync(runner, issue, scopedConfig, userConfig?.MaxRetryBackoffMs ?? 300000, cancellationToken);
            var success = runResult.Success;
            if (success)
            {
                allSuccess = allSuccess && true;
            }
            else
            {
                allSuccess = false;
                anyFailure = true;
            }

            await UpdateIssueRowAsync(issueRowId, issue, runResult, scopedConfig.Workspace.Root, cancellationToken);
            await AppendEventAsync(job.Id, issueRowId, success ? "issue.completed" : "issue.failed", runResult.Reason ?? (success ? "Completed." : "Failed."), null, cancellationToken);
        }

        string finalState;
        string? summary;
        if (issues.Count == 0)
        {
            finalState = "Completed";
            summary = "No issues.";
        }
        else if (cancellationToken.IsCancellationRequested)
        {
            finalState = "Cancelled";
            summary = "Cancelled.";
        }
        else if (allSuccess)
        {
            finalState = "Completed";
            summary = $"Completed {issues.Count} issue(s).";
        }
        else if (anyFailure)
        {
            finalState = "CompletedWithFailures";
            summary = "Completed with failures.";
        }
        else
        {
            finalState = "Completed";
            summary = $"Completed {issues.Count} issue(s).";
        }

        await MarkJobStateAsync(job.Id, finalState, null, null, JsonSerializer.Serialize(new
        {
            issueCount = issues.Count,
            summary
        }));
        await AppendEventAsync(job.Id, null, "job.completed", summary ?? finalState, null, cancellationToken);
    }

    static async Task<AgentRunResult> RunIssueWithRetryAsync(AgentRunner runner, Issue issue, ServiceConfig config, int maxBackoffMs, CancellationToken cancellationToken)
    {
        AgentRunResult result = AgentRunResult.Fail("not started");
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var liveSession = new LiveSession();
            result = await runner.RunAttemptAsync(issue, attempt, liveSession, _ => { }, cancellationToken);
            if (result.Success || cancellationToken.IsCancellationRequested)
                return result;

            if (attempt < 3)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Min(maxBackoffMs, 10000 * Math.Pow(2, attempt - 1)));
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return result;
                }
            }
        }

        return result;
    }

    bool TryResolveTicketIds(Job job, ISecretProtector secretProtector, out List<string> ticketIds)
    {
        ticketIds = [];
        var snapshot = TryParseSnapshot(job, secretProtector);
        if (snapshot is null)
            return false;

        if (!snapshot.Value.TryGetProperty("ticketIds", out var ticketNode) || ticketNode.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in ticketNode.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                ticketIds.Add(value!.Trim());
        }

        return ticketIds.Count > 0;
    }

    static JsonElement? TryParseSnapshot(Job job, ISecretProtector secretProtector)
    {
        try
        {
            var json = secretProtector.Unprotect(job.EncryptedRequestSnapshot);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var document = JsonDocument.Parse(json!);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    static List<JsonElement> ReadWorkItems(JsonElement? snapshot)
    {
        var result = new List<JsonElement>();
        if (snapshot is null || !snapshot.Value.TryGetProperty("workItems", out var workItems) || workItems.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in workItems.EnumerateArray())
            result.Add(item.Clone());

        return result;
    }

    static Issue CreateIssueFromWorkItem(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            var text = item.GetString() ?? string.Empty;
            return new Issue(Guid.NewGuid().ToString("N"), text, text, null, null, "Todo", null, null, [], [], DateTimeOffset.UtcNow, null);
        }

        string id = Guid.NewGuid().ToString("N");
        string identifier = id;
        string title = "Work item";
        string? description = null;
        int? priority = null;
        string state = "Todo";

        if (item.ValueKind == JsonValueKind.Object)
        {
            if (item.TryGetProperty("id", out var idNode) && idNode.ValueKind == JsonValueKind.String)
                id = idNode.GetString() ?? id;
            if (item.TryGetProperty("identifier", out var identifierNode) && identifierNode.ValueKind == JsonValueKind.String)
                identifier = identifierNode.GetString() ?? identifier;
            if (item.TryGetProperty("title", out var titleNode) && titleNode.ValueKind == JsonValueKind.String)
                title = titleNode.GetString() ?? title;
            if (item.TryGetProperty("description", out var descriptionNode))
                description = descriptionNode.GetString();
            if (item.TryGetProperty("priority", out var priorityNode) && priorityNode.ValueKind == JsonValueKind.Number && priorityNode.TryGetInt32(out var parsedPriority))
                priority = parsedPriority;
            if (item.TryGetProperty("state", out var stateNode) && stateNode.ValueKind == JsonValueKind.String)
                state = stateNode.GetString() ?? state;
        }

        return new Issue(id, identifier, title, description, priority, state, null, null, [], [], DateTimeOffset.UtcNow, null);
    }

    async Task<Guid> CreateIssueRowAsync(Guid jobId, Issue issue, string workspaceRoot, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = new JobIssue
        {
            JobId = jobId,
            IssueId = issue.Id,
            Identifier = issue.Identifier,
            Title = issue.Title,
            State = issue.State,
            Success = false,
            Reason = null,
            TurnCount = 0,
            StartedAt = DateTimeOffset.UtcNow,
            WorkspacePath = Path.Combine(workspaceRoot, WorkspaceManager.SanitizeKey(issue.Identifier))
        };
        db.JobIssues.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    async Task CreateFailedIssueRowAsync(Guid jobId, string issueId, string identifier, string reason, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.JobIssues.Add(new JobIssue
        {
            JobId = jobId,
            IssueId = issueId,
            Identifier = identifier,
            Title = identifier,
            State = "Failed",
            Success = false,
            Reason = reason,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    async Task UpdateIssueRowAsync(Guid issueRowId, Issue issue, AgentRunResult result, string workspaceRoot, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.JobIssues.FirstOrDefaultAsync(item => item.Id == issueRowId, cancellationToken);
        if (row is null)
            return;

        row.State = result.Success ? "Completed" : "Failed";
        row.Success = result.Success;
        row.Reason = result.Reason;
        row.CompletedAt = DateTimeOffset.UtcNow;
        row.WorkspacePath = Path.Combine(workspaceRoot, WorkspaceManager.SanitizeKey(issue.Identifier));
        await db.SaveChangesAsync(cancellationToken);
    }

    async Task MarkJobStateAsync(Guid jobId, string state, string? errorCode, string? errorMessage, string? summaryJson)
    {
        await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
        var job = await db.Jobs.FirstOrDefaultAsync(item => item.Id == jobId);
        if (job is null)
            return;

        job.State = state;
        job.ErrorCode = errorCode;
        job.ErrorMessage = errorMessage;
        if (summaryJson is not null)
            job.SummaryJson = summaryJson;
        if (state is "Completed" or "CompletedWithFailures" or "Failed" or "Cancelled")
            job.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    async Task AppendEventAsync(Guid jobId, Guid? jobIssueId, string eventName, string? summary, object? payload, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.JobEvents.Add(new JobEvent
            {
                JobId = jobId,
                JobIssueId = jobIssueId,
                Timestamp = DateTimeOffset.UtcNow,
                EventName = eventName,
                Summary = summary,
                PayloadJson = payload is null ? null : JsonSerializer.Serialize(Redaction.RedactObject(payload))
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to append event {EventName} for job {JobId}.", eventName, jobId);
        }
    }

    static ICodingAgent TryCreateCodingAgent(ServiceConfig config, ILogger logger)
    {
        try
        {
            return CodingAgentFactory.Create(config);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falling back to no-op coding agent.");
            return new NoOpCodingAgent();
        }
    }

    sealed class NoOpCodingAgent : ICodingAgent
    {
        public Task InitializeAsync(string workspacePath, LiveSession liveSession, Action<CodingAgentEvent> onEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<string?> ExecuteTurnAsync(string prompt, LiveSession liveSession, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task ShutdownAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    static string Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
