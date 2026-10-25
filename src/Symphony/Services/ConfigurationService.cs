using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Symphony.Config;
using Symphony.Data;
using Symphony.Security;
using Symphony.Workflows;
using System.Text.Json;

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
        TelemetryContentEnabled = source.TelemetryContentEnabled,
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
        target.TelemetryContentEnabled = source.TelemetryContentEnabled;
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

    public static async Task EnsureDefaultConfigurationAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        string userId,
        CancellationToken cancellationToken = default) =>
        await EnsureDefaultConfigurationAsync(factory, new SecretProtector(), userId, cancellationToken);

    public static async Task EnsureDefaultConfigurationAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        ISecretProtector protector,
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var exists = await db.UserConfigurations
            .AnyAsync(configuration => configuration.UserId == userId, cancellationToken);
        if (exists)
            return;

        var workflow = new WorkflowLoader(NullLogger<WorkflowLoader>.Instance)
            .Parse(BundledWorkflow.Content);
        var configuration = BuildFromWorkflow(workflow);
        configuration.UserId = userId;
        configuration.DefaultWorkflowName = BundledWorkflow.DefaultName;
        configuration.CreatedAt = DateTimeOffset.UtcNow;
        configuration.UpdatedAt = DateTimeOffset.UtcNow;

        await new ConfigurationService(factory, protector)
            .SaveAsync(userId, configuration, cancellationToken);
    }

    public static async Task SeedDefaultConfigurationsAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        ISecretProtector protector,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var userIds = await db.Users
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
            await EnsureDefaultConfigurationAsync(factory, protector, userId, cancellationToken);
    }

    static UserConfiguration BuildFromWorkflow(WorkflowDefinition workflow)
    {
        var config = workflow.Config;
        var result = new UserConfiguration();

        var trackerMap = ConfigReader.ReadMap(config, "tracker");
        var kind = ConfigReader.ReadString(trackerMap, "kind");
        if (string.IsNullOrWhiteSpace(kind))
            kind = "none";
        result.TrackerKind = kind.Trim().ToLowerInvariant();

        var endpoint = ConfigReader.ReadString(trackerMap, "endpoint");
        if (string.IsNullOrWhiteSpace(endpoint) && string.Equals(result.TrackerKind, "github", StringComparison.OrdinalIgnoreCase))
            endpoint = "https://api.github.com";
        if (string.IsNullOrWhiteSpace(endpoint) && result.TrackerKind == "linear")
            endpoint = "https://api.linear.app/graphql";
        result.TrackerEndpoint = endpoint ?? string.Empty;

        var apiKeyRaw = ConfigReader.ReadString(trackerMap, "api_key");
        result.TrackerApiKey = string.IsNullOrWhiteSpace(apiKeyRaw)
            ? string.Empty
            : ConfigReader.ResolveEnvValue(apiKeyRaw);

        result.TrackerProjectSlug = ConfigReader.ReadString(trackerMap, "project_slug") ?? string.Empty;

        var activeStates = ConfigReader.ReadStringList(trackerMap, "active_states");
        result.TrackerActiveStates = activeStates is null
            ? "Todo, In Progress"
            : string.Join(", ", activeStates);

        var terminalStates = ConfigReader.ReadStringList(trackerMap, "terminal_states");
        result.TrackerTerminalStates = terminalStates is null
            ? "Closed, Cancelled, Canceled, Duplicate, Done"
            : string.Join(", ", terminalStates);

        var hooksMap = ConfigReader.ReadMap(config, "hooks");
        if (hooksMap.Count > 0)
        {
            result.HookAfterCreate = ConfigReader.ReadString(hooksMap, "after_create");
            result.HookBeforeRun = ConfigReader.ReadString(hooksMap, "before_run");
            result.HookAfterRun = ConfigReader.ReadString(hooksMap, "after_run");
            result.HookBeforeRemove = ConfigReader.ReadString(hooksMap, "before_remove");
            var timeout = ConfigReader.ReadInt(hooksMap, "timeout_ms");
            if (timeout.HasValue && timeout.Value > 0)
                result.HookTimeoutMs = timeout.Value;
        }

        var agentMap = ConfigReader.ReadMap(config, "agent");
        var maxConcurrent = ConfigReader.ReadInt(agentMap, "max_concurrent_agents")
            ?? ConfigReader.ReadInt(config, "max_concurrent_agents");
        if (maxConcurrent.HasValue && maxConcurrent.Value > 0)
            result.MaxConcurrentAgents = maxConcurrent.Value;

        var backoff = ConfigReader.ReadInt(agentMap, "max_retry_backoff_ms")
            ?? ConfigReader.ReadInt(config, "max_retry_backoff_ms");
        if (backoff.HasValue && backoff.Value > 0)
            result.MaxRetryBackoffMs = backoff.Value;

        var maxTurns = ConfigReader.ReadInt(agentMap, "max_turns")
            ?? ConfigReader.ReadInt(config, "max_turns");
        if (maxTurns.HasValue && maxTurns.Value > 0)
            result.MaxTurns = maxTurns.Value;

        if (agentMap.TryGetValue("max_concurrent_agents_by_state", out var byStateRaw)
            && byStateRaw is IReadOnlyDictionary<string, object> byStateMap)
        {
            result.MaxConcurrentAgentsByState = JsonSerializer.Serialize(byStateMap);
        }

        if (result.HookTimeoutMs <= 0)
            result.HookTimeoutMs = 60000;
        if (result.MaxConcurrentAgents <= 0)
            result.MaxConcurrentAgents = 10;
        if (result.MaxRetryBackoffMs <= 0)
            result.MaxRetryBackoffMs = 300000;
        if (result.MaxTurns <= 0)
            result.MaxTurns = 1;

        return result;
    }
}