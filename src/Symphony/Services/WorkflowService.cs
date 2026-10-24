using Microsoft.EntityFrameworkCore;
using Symphony.Data;

namespace Symphony.Services;

public sealed class WorkflowService(IDbContextFactory<SymphonyDbContext> factory)
{
    public const string DefaultWorkflowName = BundledWorkflow.DefaultName;

    readonly IDbContextFactory<SymphonyDbContext> factory =
        factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task<IReadOnlyList<Workflow>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);

        // Lazily seed the default workflow for newly created or legacy users.
        try { await EnsureDefaultWorkflowAsync(userId, cancellationToken); }
        catch { /* seeding failures should not block listing */ }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.Workflows
            .AsNoTracking()
            .Where(workflow => workflow.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        return items
            .OrderBy(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<IReadOnlyList<Workflow>> ListWorkflowsAsync(string userId, CancellationToken cancellationToken = default) =>
        ListAsync(userId, cancellationToken);

    public Task<IReadOnlyList<Workflow>> GetAllAsync(string userId, CancellationToken cancellationToken = default) =>
        ListAsync(userId, cancellationToken);

    public Task<IReadOnlyList<Workflow>> GetWorkflowsAsync(string userId, CancellationToken cancellationToken = default) =>
        ListAsync(userId, cancellationToken);

    public async Task<Workflow?> GetAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);
        var trimmed = NormalizeName(name);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.Workflows
            .AsNoTracking()
            .Where(workflow => workflow.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        return items.FirstOrDefault(workflow =>
            string.Equals(workflow.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public Task<Workflow?> GetByNameAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        GetAsync(userId, name, cancellationToken);

    public Task<Workflow?> FindAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        GetAsync(userId, name, cancellationToken);

    public Task<Workflow?> LoadAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        GetAsync(userId, name, cancellationToken);

    public async Task<bool> ExistsAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);
        var trimmed = NormalizeName(name);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.Workflows
            .Where(workflow => workflow.OwnerUserId == userId)
            .Select(workflow => workflow.Name)
            .ToListAsync(cancellationToken);

        return items.Any(stored =>
            string.Equals(stored, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Workflow> CreateAsync(
        string userId,
        string name,
        string content,
        bool isDefault = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);
        var trimmed = NormalizeName(name);
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Workflow content cannot be empty.", nameof(content));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var existingNames = await db.Workflows
            .Where(workflow => workflow.OwnerUserId == userId)
            .Select(workflow => workflow.Name)
            .ToListAsync(cancellationToken);

        if (existingNames.Any(stored =>
                string.Equals(stored, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new WorkflowConflictException("duplicate_name", "A workflow with that name already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new Workflow
        {
            OwnerUserId = userId,
            Name = trimmed,
            Content = content,
            IsDefault = isDefault,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Workflows.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new WorkflowConflictException(
                "duplicate_name",
                "A workflow with that name already exists.",
                exception);
        }

        return entity;
    }

    public Task<Workflow> CreateWorkflowAsync(
        string userId,
        string name,
        string content,
        bool isDefault = false,
        CancellationToken cancellationToken = default) =>
        CreateAsync(userId, name, content, isDefault, cancellationToken);

    public Task<Workflow> AddAsync(
        string userId,
        string name,
        string content,
        bool isDefault = false,
        CancellationToken cancellationToken = default) =>
        CreateAsync(userId, name, content, isDefault, cancellationToken);

    public async Task<Workflow?> UpdateAsync(
        string userId,
        string name,
        string content,
        bool? isDefault = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);
        var trimmed = NormalizeName(name);
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Workflow content cannot be empty.", nameof(content));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.Workflows
            .Where(workflow => workflow.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        var entity = items.FirstOrDefault(workflow =>
            string.Equals(workflow.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (entity is null)
            return null;

        entity.Content = content;
        if (isDefault.HasValue)
            entity.IsDefault = isDefault.Value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<Workflow?> UpdateAsync(
        string userId,
        string name,
        Workflow workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);

        var oldName = NormalizeName(name);
        var requestedName = string.IsNullOrWhiteSpace(workflow.Name)
            ? oldName
            : NormalizeName(workflow.Name);

        if (string.IsNullOrWhiteSpace(workflow.Content))
            throw new ArgumentException("Workflow content cannot be empty.", nameof(workflow));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.Workflows
            .Where(stored => stored.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        var entity = items.FirstOrDefault(stored =>
            string.Equals(stored.Name, oldName, StringComparison.OrdinalIgnoreCase));

        if (entity is null)
            return null;

        if (!string.Equals(requestedName, oldName, StringComparison.OrdinalIgnoreCase) &&
            items.Any(stored =>
                !string.Equals(stored.Name, oldName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(stored.Name, requestedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new WorkflowConflictException(
                "duplicate_name",
                "A workflow with that name already exists.");
        }

        entity.Name = requestedName;
        entity.Content = workflow.Content;
        entity.IsDefault = workflow.IsDefault;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<Workflow?> UpdateWorkflowAsync(
        string userId,
        string name,
        string content,
        bool? isDefault = null,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(userId, name, content, isDefault, cancellationToken);

    public async Task<Workflow> SaveAsync(string userId, Workflow workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);

        var trimmed = NormalizeName(workflow.Name);

        var existing = await GetAsync(userId, trimmed, cancellationToken);
        if (existing is null)
        {
            return await CreateAsync(userId, trimmed, workflow.Content, workflow.IsDefault, cancellationToken);
        }

        existing.Content = workflow.Content;
        existing.IsDefault = workflow.IsDefault;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var tracked = await db.Workflows.FirstOrDefaultAsync(
            item => item.Id == existing.Id,
            cancellationToken);

        if (tracked is not null)
        {
            tracked.Content = existing.Content;
            tracked.IsDefault = existing.IsDefault;
            tracked.UpdatedAt = existing.UpdatedAt;
            await db.SaveChangesAsync(cancellationToken);
        }

        return existing;
    }

    public Task<Workflow> UpsertAsync(string userId, Workflow workflow, CancellationToken cancellationToken = default) =>
        SaveAsync(userId, workflow, cancellationToken);

    public async Task<bool> DeleteAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUserId(userId);
        var trimmed = NormalizeName(name);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var items = await db.Workflows
            .Where(workflow => workflow.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        var entity = items.FirstOrDefault(workflow =>
            string.Equals(workflow.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (entity is null)
            return false;

        if (items.Count <= 1)
            throw new WorkflowConflictException("last_workflow", "Cannot delete the last remaining workflow.");

        db.Workflows.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> DeleteWorkflowAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        DeleteAsync(userId, name, cancellationToken);

    public Task<bool> RemoveAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        DeleteAsync(userId, name, cancellationToken);

    public Task<Workflow?> GetWorkflowAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        GetAsync(userId, name, cancellationToken);

    public Task<Workflow> CreateAsync(string userId, Workflow workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return CreateAsync(userId, workflow.Name, workflow.Content, workflow.IsDefault, cancellationToken);
    }

    public Task<Workflow?> UpdateAsync(string userId, Workflow workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return UpdateAsync(userId, workflow.Name, workflow.Content, workflow.IsDefault, cancellationToken);
    }

    public async Task<Workflow> SaveAsync(
        string userId,
        string name,
        string content,
        bool isDefault = false,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(userId, name, cancellationToken);
        if (existing is null)
            return await CreateAsync(userId, name, content, isDefault, cancellationToken);

        var updated = await UpdateAsync(userId, name, content, isDefault, cancellationToken);
        return updated ?? existing;
    }

    public Task<Workflow> UpsertAsync(
        string userId,
        string name,
        string content,
        bool isDefault = false,
        CancellationToken cancellationToken = default) =>
        SaveAsync(userId, name, content, isDefault, cancellationToken);

    public static string DefaultWorkflowContent => BundledWorkflow.Content;

    public static string GetDefaultWorkflowContent() => BundledWorkflow.Content;

    public static async Task EnsureDefaultWorkflowAsync(
        SymphonyDbContext db,
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId));

        var exists = await db.Workflows
            .AnyAsync(workflow => workflow.OwnerUserId == userId, cancellationToken);
        if (exists)
            return;

        var now = DateTimeOffset.UtcNow;
        db.Workflows.Add(new Workflow
        {
            OwnerUserId = userId,
            Name = DefaultWorkflowName,
            Content = DefaultWorkflowContent,
            IsDefault = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public static Task EnsureDefaultAsync(
        SymphonyDbContext db,
        string userId,
        CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(db, userId, cancellationToken);

    public static Task SeedDefaultWorkflowAsync(
        SymphonyDbContext db,
        string userId,
        CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(db, userId, cancellationToken);

    public static Task SeedDefaultAsync(
        SymphonyDbContext db,
        string userId,
        CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(db, userId, cancellationToken);

    public static async Task EnsureDefaultWorkflowAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await EnsureDefaultWorkflowAsync(db, userId, cancellationToken);
    }

    public static Task EnsureDefaultAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        string userId,
        CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(factory, userId, cancellationToken);

    public static Task SeedDefaultWorkflowAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        string userId,
        CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(factory, userId, cancellationToken);

    public static Task SeedDefaultAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        string userId,
        CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(factory, userId, cancellationToken);

    public async Task EnsureDefaultWorkflowAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultWorkflowAsync(factory, userId, cancellationToken);
    }

    public Task EnsureDefaultAsync(string userId, CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(userId, cancellationToken);

    public Task SeedDefaultWorkflowAsync(string userId, CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(userId, cancellationToken);

    public Task SeedDefaultAsync(string userId, CancellationToken cancellationToken = default) =>
        EnsureDefaultWorkflowAsync(userId, cancellationToken);

    public Task SeedDefaultWorkflowForAllUsersAsync(CancellationToken cancellationToken = default) =>
        SeedDefaultWorkflowsForAllUsersAsync(factory, cancellationToken);

    public static async Task SeedDefaultWorkflowsForAllUsersAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var userIds = await db.Users
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
            await EnsureDefaultWorkflowAsync(db, userId, cancellationToken);
    }

    public static async Task SeedDefaultWorkflowsForAllUsersAsync(
        SymphonyDbContext db,
        CancellationToken cancellationToken = default)
    {
        var userIds = await db.Users
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
            await EnsureDefaultWorkflowAsync(db, userId, cancellationToken);
    }


    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
    }

    static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name cannot be empty.", nameof(name));

        return name.Trim();
    }
}

public sealed class WorkflowConflictException : InvalidOperationException
{
    public WorkflowConflictException(string code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    public string Code { get; }
}
