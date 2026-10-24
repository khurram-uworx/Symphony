using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Symphony.Workspaces;
using Symphony.Data;
using Symphony.Dto;
using Symphony.Agent;
using Symphony.Domain;
using Symphony.Processing;
using Symphony.Security;

namespace Symphony.Services;

public sealed class JobService
{
    readonly IDbContextFactory<SymphonyDbContext> factory;
    readonly ISecretProtector secretProtector;
    readonly JobQueue? jobQueue;

    public JobService(IDbContextFactory<SymphonyDbContext> factory)
        : this(factory, secretProtector: null, jobQueue: null)
    {
    }

    public JobService(IDbContextFactory<SymphonyDbContext> factory, ISecretProtector? secretProtector)
        : this(factory, secretProtector, null)
    {
    }

    public JobService(IDbContextFactory<SymphonyDbContext> factory, JobQueue? jobQueue)
        : this(factory, null, jobQueue)
    {
    }

    public JobService(
        IDbContextFactory<SymphonyDbContext> factory,
        ISecretProtector? secretProtector,
        JobQueue? jobQueue,
        ILogger<JobService>? logger = null)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.secretProtector = secretProtector ?? new SecretProtector();
        this.jobQueue = jobQueue;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<JobService>.Instance;
    }

    IDbContextFactory<SymphonyDbContext> Factory => factory;

    static void ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
    }

    public Task<Job> CreateAsync(
        string userId,
        WorkRequest request,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    public Task<Job> CreateJobAsync(string userId, WorkRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    public Task<Job> SubmitAsync(string userId, WorkRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    public Task<Job> SubmitWorkAsync(string userId, WorkRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    public Task<Job> QueueAsync(string userId, WorkRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    public Task<Job> EnqueueAsync(string userId, WorkRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    public Task<Job> AddAsync(string userId, WorkRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    public Task<Job> Create(string userId, WorkRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    public Task<Job> Submit(string userId, WorkRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(userId, request, cancellationToken);

    async Task<Job> CreateCoreAsync(
        string userId,
        WorkRequest? request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);

        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (!request.IsValid(out var validationError))
            throw new ArgumentException(validationError, nameof(request));

        try { await new WorkflowService(factory).EnsureDefaultWorkflowAsync(userId, cancellationToken); }
        catch { /* default seeding is best-effort */ }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var workflows = await db.Workflows
            .AsNoTracking()
            .Where(workflow => workflow.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        var requestedName = request.WorkflowName!.Trim();
        var workflow = workflows.FirstOrDefault(stored =>
            string.Equals(stored.Name, requestedName, StringComparison.OrdinalIgnoreCase));

        if (workflow is null)
            throw new ArgumentException("Workflow not found or not owned by requester.", nameof(request));

        var snapshot = new
        {
            externalId = request.ExternalId,
            ticketIds = request.TicketIds,
            workItems = request.WorkItems,
            github = new
            {
                repository = request.GitHub!.Repository,
                personalAccessToken = request.GitHub.PersonalAccessToken
            },
            workflowName = request.WorkflowName,
            overrides = request.Overrides
        };

        var snapshotJson = JsonSerializer.Serialize(
            snapshot,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var job = new Job
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId!.Trim(),
            WorkflowName = workflow.Name,
            Repository = request.GitHub.Repository!.Trim(),
            EncryptedRequestSnapshot = secretProtector.Protect(snapshotJson),
            State = "Queued",
            QueuedAt = DateTimeOffset.UtcNow
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        try { await (jobQueue?.SignalInstanceAsync(cancellationToken) ?? Task.CompletedTask); }
        catch { /* best-effort wake signal */ }

        return job;
    }

    public async Task<Guid> CreateIssueRowAsync(
        Guid jobId,
        Issue issue,
        string workspaceRoot,
        CancellationToken cancellationToken = default) =>
        await CreateJobIssueAsync(jobId, issue, workspaceRoot, cancellationToken);

    public async Task<Guid> CreateJobIssueAsync(
        Guid jobId,
        Issue issue,
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
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

    public Task<Guid> CreateIssueAsync(Guid jobId, Issue issue, string workspaceRoot, CancellationToken cancellationToken = default) =>
        CreateJobIssueAsync(jobId, issue, workspaceRoot, cancellationToken);

    public Task<Guid> AddIssueAsync(Guid jobId, Issue issue, string workspaceRoot, CancellationToken cancellationToken = default) =>
        CreateJobIssueAsync(jobId, issue, workspaceRoot, cancellationToken);

    public async Task CreateFailedIssueRowAsync(
        Guid jobId,
        string issueId,
        string identifier,
        string reason,
        CancellationToken cancellationToken = default) =>
        await CreateFailedJobIssueAsync(jobId, issueId, identifier, reason, cancellationToken);

    public async Task CreateFailedJobIssueAsync(
        Guid jobId,
        string issueId,
        string identifier,
        string reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.JobIssues.Add(new JobIssue
        {
            JobId = jobId,
            IssueId = issueId,
            Identifier = identifier,
            Title = identifier,
            State = "Failed",
            Success = false,
            Reason = reason,
            TurnCount = 0,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task CreateFailedIssueAsync(Guid jobId, string issueId, string identifier, string reason, CancellationToken cancellationToken = default) =>
        CreateFailedJobIssueAsync(jobId, issueId, identifier, reason, cancellationToken);

    public Task AddFailedIssueAsync(Guid jobId, string issueId, string identifier, string reason, CancellationToken cancellationToken = default) =>
        CreateFailedJobIssueAsync(jobId, issueId, identifier, reason, cancellationToken);

    public async Task UpdateIssueRowAsync(
        Guid issueRowId,
        Issue issue,
        AgentRunResult result,
        string workspaceRoot,
        CancellationToken cancellationToken = default) =>
        await UpdateJobIssueAsync(issueRowId, issue, result, workspaceRoot, cancellationToken);

    public async Task UpdateJobIssueAsync(
        Guid issueRowId,
        Issue issue,
        AgentRunResult result,
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await db.JobIssues.FirstOrDefaultAsync(item => item.Id == issueRowId, cancellationToken);
        if (row is null)
            return;

        row.State = result.Success ? "Completed" : "Failed";
        row.Success = result.Success;
        row.Reason = result.Reason;
        row.TurnCount = result.TurnCount;
        row.CompletedAt = DateTimeOffset.UtcNow;
        row.WorkspacePath = Path.Combine(workspaceRoot, WorkspaceManager.SanitizeKey(issue.Identifier));
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateIssueAsync(Guid issueRowId, Issue issue, AgentRunResult result, string workspaceRoot, CancellationToken cancellationToken = default) =>
        UpdateJobIssueAsync(issueRowId, issue, result, workspaceRoot, cancellationToken);

    public async Task MarkJobStateAsync(
        Guid jobId,
        string state,
        string? errorCode,
        string? errorMessage,
        string? summaryJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken == CancellationToken.None ? CancellationToken.None : cancellationToken);
        var job = await db.Jobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null)
            return;

        job.State = state;
        job.ErrorCode = errorCode;
        job.ErrorMessage = errorMessage;
        if (summaryJson is not null)
            job.SummaryJson = summaryJson;
        if (state is "Completed" or "CompletedWithFailures" or "Failed" or "Cancelled")
            job.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateJobStateAsync(Guid jobId, string state, string? errorCode, string? errorMessage, string? summaryJson, CancellationToken cancellationToken = default) =>
        MarkJobStateAsync(jobId, state, errorCode, errorMessage, summaryJson, cancellationToken);

    public Task SetJobStateAsync(Guid jobId, string state, string? errorCode, string? errorMessage, string? summaryJson, CancellationToken cancellationToken = default) =>
        MarkJobStateAsync(jobId, state, errorCode, errorMessage, summaryJson, cancellationToken);

    public async Task AppendEventAsync(
        Guid jobId,
        Guid? jobIssueId,
        string eventName,
        string? summary,
        object? payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
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
            logger?.LogWarning(ex, "Failed to append event {EventName} for job {JobId}.", eventName, jobId);
        }
    }

    public Task AppendJobEventAsync(Guid jobId, Guid? jobIssueId, string eventName, string? summary, object? payload, CancellationToken cancellationToken = default) =>
        AppendEventAsync(jobId, jobIssueId, eventName, summary, payload, cancellationToken);

    public Task AddEventAsync(Guid jobId, Guid? jobIssueId, string eventName, string? summary, object? payload, CancellationToken cancellationToken = default) =>
        AppendEventAsync(jobId, jobIssueId, eventName, summary, payload, cancellationToken);

    public Task RecordEventAsync(Guid jobId, Guid? jobIssueId, string eventName, string? summary, object? payload, CancellationToken cancellationToken = default) =>
        AppendEventAsync(jobId, jobIssueId, eventName, summary, payload, cancellationToken);

    public async Task<Symphony.Data.Job?> GetJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Jobs
            .AsNoTracking()
            .Include(job => job.Issues)
            .Include(job => job.Events)
            .FirstOrDefaultAsync(job => job.Id == jobId, cancellationToken);
    }

    public Task<Symphony.Data.Job?> GetJobAsync(
        string userId,
        Guid jobId,
        bool isAdmin,
        CancellationToken cancellationToken = default) =>
        GetByKeyCoreAsync(userId, jobId, isAdmin, cancellationToken);

    async Task<Symphony.Data.Job?> GetByKeyCoreAsync(
        string userId,
        Guid jobId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var job = await GetJobAsync(jobId, cancellationToken);
        if (job is null)
            return null;
        if (!isAdmin && job.OwnerUserId != userId)
            return null;
        return job;
    }

    public Task<Symphony.Data.Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        GetJobAsync(jobId, cancellationToken);

    public Task<Symphony.Data.Job?> FindJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        GetJobAsync(jobId, cancellationToken);

    public Task<Symphony.Data.Job?> LoadJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        GetJobAsync(jobId, cancellationToken);

    public Task<Symphony.Data.Job?> GetAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        GetJobAsync(jobId, cancellationToken);

    public Task<Symphony.Data.Job?> GetAsync(
        string userId,
        Guid jobId,
        bool isAdmin,
        CancellationToken cancellationToken = default) =>
        GetJobAsync(userId, jobId, isAdmin, cancellationToken);

    public Task<Symphony.Data.Job?> GetJobByIdAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        GetJobAsync(jobId, cancellationToken);

    async Task<Symphony.Data.Job?> GetByKeyWithCheckAsync(
        string userId,
        Guid jobId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var job = await GetJobAsync(jobId, cancellationToken);
        if (job is null)
            return null;
        if (!isAdmin && job.OwnerUserId != userId)
            return null;
        return job;
    }

    public Task<IReadOnlyList<Symphony.Data.Job>> ListAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        ListByOwnerAsync(userId, cancellationToken);

    async Task<IReadOnlyList<Symphony.Data.Job>> ListByOwnerAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Jobs
            .AsNoTracking()
            .Where(job => job.OwnerUserId == userId)
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(job => job.QueuedAt).ToList();
    }

    public Task<IReadOnlyList<Symphony.Data.Job>> ListAsync(
        CancellationToken cancellationToken = default) =>
        ListAllAsync(cancellationToken);

    async Task<IReadOnlyList<Symphony.Data.Job>> ListAllAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Jobs
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(job => job.QueuedAt).ToList();
    }

    public Task<IReadOnlyList<Symphony.Data.Job>> ListAllJobsAsync(
        CancellationToken cancellationToken = default) =>
        ListAllAsync(cancellationToken);

    public Task<IReadOnlyList<Symphony.Data.Job>> ListJobsAsync(
        CancellationToken cancellationToken = default) =>
        ListAllAsync(cancellationToken);

    public Task<IReadOnlyList<Symphony.Data.Job>> ListJobsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        ListByOwnerAsync(userId, cancellationToken);

    public Task<IReadOnlyList<Symphony.Data.Job>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        ListAllAsync(cancellationToken);

    public Task<IReadOnlyList<Symphony.Data.Job>> GetAllAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        ListByOwnerAsync(userId, cancellationToken);

    public Task<IReadOnlyList<Symphony.Data.Job>> GetAllJobsAsync(
        CancellationToken cancellationToken = default) =>
        ListAllAsync(cancellationToken);

    public Task<IReadOnlyList<Symphony.Data.Job>> GetAllJobsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        ListByOwnerAsync(userId, cancellationToken);

    public Task<IReadOnlyList<Symphony.Data.Job>> FindAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        ListByOwnerAsync(userId, cancellationToken);

    public Task<IReadOnlyList<Symphony.Data.Job>> QueryAsync(
        CancellationToken cancellationToken = default) =>
        ListAllAsync(cancellationToken);

    public async Task<IReadOnlyList<Symphony.Data.Job>> QueryJobsAsync(
        string? ownerUserId = null,
        string? state = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Jobs.AsNoTracking().ToListAsync(cancellationToken);
        var filtered = rows.Where(job =>
            (string.IsNullOrWhiteSpace(ownerUserId) || job.OwnerUserId == ownerUserId) &&
            (string.IsNullOrWhiteSpace(state) || job.State == state) &&
            (!from.HasValue || job.QueuedAt >= from.Value) &&
            (!to.HasValue || job.QueuedAt <= to.Value));
        var ordered = filtered.OrderByDescending(job => job.QueuedAt).ToList();
        if (pageSize.HasValue && pageSize.Value > 0)
        {
            var normalizedPage = page.HasValue && page.Value > 0 ? page.Value : 1;
            ordered = ordered.Skip((normalizedPage - 1) * pageSize.Value).Take(pageSize.Value).ToList();
        }
        return ordered;
    }

    async Task<IReadOnlyList<Symphony.Data.Job>> QueryCoreAsync(
        string? ownerUserId,
        string? state,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? skip,
        int? take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Jobs.AsNoTracking().ToListAsync(cancellationToken);
        var filtered = rows.Where(job =>
            (string.IsNullOrWhiteSpace(ownerUserId) || job.OwnerUserId == ownerUserId) &&
            (string.IsNullOrWhiteSpace(state) || job.State == state) &&
            (!from.HasValue || job.QueuedAt >= from.Value) &&
            (!to.HasValue || job.QueuedAt <= to.Value))
            .OrderByDescending(job => job.QueuedAt)
            .ToList();
        if (skip.HasValue && skip.Value > 0)
            filtered = filtered.Skip(skip.Value).ToList();
        if (take.HasValue && take.Value > 0)
            filtered = filtered.Take(take.Value).ToList();
        return filtered;
    }

    public Task<int> CountJobsAsync(
        string? ownerUserId = null,
        string? state = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default) =>
        CountCoreAsync(ownerUserId, state, from, to, cancellationToken);

    async Task<int> CountCoreAsync(
        string? ownerUserId,
        string? state,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Jobs.AsNoTracking().ToListAsync(cancellationToken);
        return rows.Count(job =>
            (string.IsNullOrWhiteSpace(ownerUserId) || job.OwnerUserId == ownerUserId) &&
            (string.IsNullOrWhiteSpace(state) || job.State == state) &&
            (!from.HasValue || job.QueuedAt >= from.Value) &&
            (!to.HasValue || job.QueuedAt <= to.Value));
    }

    public async Task<IReadOnlyList<Symphony.Data.JobIssue>> GetIssuesAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.JobIssues
            .AsNoTracking()
            .Where(issue => issue.JobId == jobId)
            .ToListAsync(cancellationToken);
        return rows.OrderBy(issue => issue.StartedAt ?? DateTimeOffset.MaxValue).ToList();
    }

    public async Task<IReadOnlyList<Symphony.Data.JobEvent>> GetEventsAsync(
        Guid jobId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.JobEvents
            .AsNoTracking()
            .Where(item => item.JobId == jobId)
            .ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(item => item.Timestamp)
            .Take(take > 0 ? take : 100)
            .ToList();
    }

    public async Task<bool> CancelAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var job = await db.Jobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null)
            return false;
        if (job.State is "Completed" or "CompletedWithFailures" or "Failed" or "Cancelled")
            return false;
        await MarkJobStateAsync(jobId, "Cancelled", null, "Job cancelled.", null, cancellationToken);
        await AppendEventAsync(jobId, null, "job.cancelled", "Job cancelled.", null, cancellationToken);
        return true;
    }

    public Task<bool> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        CancelAsync(jobId, cancellationToken);

    public Task<bool> TryCancelAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        CancelAsync(jobId, cancellationToken);

    readonly ILogger<JobService> logger;
}

public static class JobServiceExtensions
{
    // placeholder for future static helpers
}