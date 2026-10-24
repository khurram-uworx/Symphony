using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Symphony.Data;
using Symphony.Dto;
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
        JobQueue? jobQueue)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.secretProtector = secretProtector ?? new SecretProtector();
        this.jobQueue = jobQueue;
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
}