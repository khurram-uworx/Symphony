using Microsoft.EntityFrameworkCore;
using Symphony.Data;
using Symphony.Security;

namespace Symphony.Services;

public sealed class ConfigurationService(
    IDbContextFactory<SymphonyDbContext> factory,
    ISecretProtector? protector = null)
{
    readonly IDbContextFactory<SymphonyDbContext> factory = factory ?? throw new ArgumentNullException(nameof(factory));
    readonly ISecretProtector protector = protector ?? new SecretProtector();

    public async Task<UserConfiguration?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await db.UserConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(configuration => configuration.UserId == userId, cancellationToken);

        if (entity is null)
            return null;

        entity.TrackerApiKey = protector.Unprotect(entity.TrackerApiKey) ?? string.Empty;
        entity.CopilotApiKey = protector.Unprotect(entity.CopilotApiKey);
        entity.GitHubPersonalAccessToken = protector.Unprotect(entity.GitHubPersonalAccessToken);
        ApplyDefaults(entity);
        return entity;
    }

    public Task<UserConfiguration?> GetForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        GetAsync(userId, cancellationToken);

    public Task<UserConfiguration?> GetConfigurationAsync(string userId, CancellationToken cancellationToken = default) =>
        GetAsync(userId, cancellationToken);

    public Task<UserConfiguration?> FindAsync(string userId, CancellationToken cancellationToken = default) =>
        GetAsync(userId, cancellationToken);

    public Task<UserConfiguration?> LoadAsync(string userId, CancellationToken cancellationToken = default) =>
        GetAsync(userId, cancellationToken);

    public async Task<UserConfiguration> SaveAsync(
        string userId,
        UserConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var existing = await db.UserConfigurations
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (existing is null)
        {
            var entity = Clone(configuration, userId);
            entity.TrackerApiKey = protector.Protect(configuration.TrackerApiKey) ?? string.Empty;
            entity.CopilotApiKey = protector.Protect(configuration.CopilotApiKey);
            entity.GitHubPersonalAccessToken = protector.Protect(configuration.GitHubPersonalAccessToken);
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            ApplyDefaults(entity);
            db.UserConfigurations.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            Copy(configuration, existing);
            existing.UserId = userId;
            existing.TrackerApiKey = protector.Protect(configuration.TrackerApiKey) ?? string.Empty;
            existing.CopilotApiKey = protector.Protect(configuration.CopilotApiKey);
            existing.GitHubPersonalAccessToken = protector.Protect(configuration.GitHubPersonalAccessToken);
            existing.UpdatedAt = now;
            ApplyDefaults(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        var result = Clone(configuration, userId);
        result.TrackerApiKey = configuration.TrackerApiKey;
        result.CopilotApiKey = configuration.CopilotApiKey;
        result.GitHubPersonalAccessToken = configuration.GitHubPersonalAccessToken;
        result.CreatedAt = existing?.CreatedAt ?? now;
        result.UpdatedAt = now;
        ApplyDefaults(result);
        return result;
    }

    public Task<UserConfiguration> UpsertAsync(string userId, UserConfiguration configuration, CancellationToken cancellationToken = default) =>
        SaveAsync(userId, configuration, cancellationToken);

    public Task<UserConfiguration> UpdateAsync(string userId, UserConfiguration configuration, CancellationToken cancellationToken = default) =>
        SaveAsync(userId, configuration, cancellationToken);

    public Task<UserConfiguration> CreateOrUpdateAsync(string userId, UserConfiguration configuration, CancellationToken cancellationToken = default) =>
        SaveAsync(userId, configuration, cancellationToken);

    public Task<UserConfiguration> SaveForUserAsync(string userId, UserConfiguration configuration, CancellationToken cancellationToken = default) =>
        SaveAsync(userId, configuration, cancellationToken);

    static UserConfiguration Clone(UserConfiguration source, string userId) => new()
    {
        Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString() : source.Id,
        UserId = userId,
        TrackerKind = source.TrackerKind,
        TrackerEndpoint = source.TrackerEndpoint,
        TrackerApiKey = source.TrackerApiKey,
        TrackerProjectSlug = source.TrackerProjectSlug,
        TrackerActiveStates = source.TrackerActiveStates,
        TrackerTerminalStates = source.TrackerTerminalStates,
        WorkspaceRoot = source.WorkspaceRoot,
        HookAfterCreate = source.HookAfterCreate,
        HookBeforeRun = source.HookBeforeRun,
        HookAfterRun = source.HookAfterRun,
        HookBeforeRemove = source.HookBeforeRemove,
        HookTimeoutMs = source.HookTimeoutMs,
        MaxConcurrentAgents = source.MaxConcurrentAgents,
        MaxRetryBackoffMs = source.MaxRetryBackoffMs,
        MaxTurns = source.MaxTurns,
        MaxConcurrentAgentsByState = source.MaxConcurrentAgentsByState,
        CopilotProvider = source.CopilotProvider,
        CopilotBaseUrl = source.CopilotBaseUrl,
        CopilotModelId = source.CopilotModelId,
        CopilotApiKey = source.CopilotApiKey,
        GitHubPersonalAccessToken = source.GitHubPersonalAccessToken,
        CopilotWireApi = source.CopilotWireApi,
        DefaultWorkflowName = source.DefaultWorkflowName,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt
    };

    static void Copy(UserConfiguration source, UserConfiguration target)
    {
        target.TrackerKind = source.TrackerKind;
        target.TrackerEndpoint = source.TrackerEndpoint;
        target.TrackerProjectSlug = source.TrackerProjectSlug;
        target.TrackerActiveStates = source.TrackerActiveStates;
        target.TrackerTerminalStates = source.TrackerTerminalStates;
        target.WorkspaceRoot = source.WorkspaceRoot;
        target.HookAfterCreate = source.HookAfterCreate;
        target.HookBeforeRun = source.HookBeforeRun;
        target.HookAfterRun = source.HookAfterRun;
        target.HookBeforeRemove = source.HookBeforeRemove;
        target.HookTimeoutMs = source.HookTimeoutMs;
        target.MaxConcurrentAgents = source.MaxConcurrentAgents;
        target.MaxRetryBackoffMs = source.MaxRetryBackoffMs;
        target.MaxTurns = source.MaxTurns;
        target.MaxConcurrentAgentsByState = source.MaxConcurrentAgentsByState;
        target.CopilotProvider = source.CopilotProvider;
        target.CopilotBaseUrl = source.CopilotBaseUrl;
        target.CopilotModelId = source.CopilotModelId;
        target.CopilotWireApi = source.CopilotWireApi;
        target.GitHubPersonalAccessToken = source.GitHubPersonalAccessToken;
        target.DefaultWorkflowName = source.DefaultWorkflowName;
    }

    static void ApplyDefaults(UserConfiguration configuration)
    {
        if (configuration.MaxConcurrentAgents <= 0)
            configuration.MaxConcurrentAgents = 10;
        if (configuration.MaxRetryBackoffMs <= 0)
            configuration.MaxRetryBackoffMs = 300000;
        if (configuration.MaxTurns <= 0)
            configuration.MaxTurns = 1;
        if (configuration.HookTimeoutMs <= 0)
            configuration.HookTimeoutMs = 60000;
    }

    public static Task<UserConfiguration?> GetAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        string userId,
        CancellationToken cancellationToken = default) =>
        new ConfigurationService(factory).GetAsync(userId, cancellationToken);

    public static Task<UserConfiguration?> GetAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        ISecretProtector protector,
        string userId,
        CancellationToken cancellationToken = default) =>
        new ConfigurationService(factory, protector).GetAsync(userId, cancellationToken);

    public static Task<UserConfiguration> SaveAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        string userId,
        UserConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        new ConfigurationService(factory).SaveAsync(userId, configuration, cancellationToken);

    public static Task<UserConfiguration> SaveAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        ISecretProtector protector,
        string userId,
        UserConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        new ConfigurationService(factory, protector).SaveAsync(userId, configuration, cancellationToken);
}