using Microsoft.AspNetCore.Identity;

namespace Symphony.Data;

public sealed class UserConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = string.Empty;

    public IdentityUser? User { get; set; }

    public string TrackerKind { get; set; } = string.Empty;

    public string TrackerEndpoint { get; set; } = string.Empty;

    public string TrackerApiKey { get; set; } = string.Empty;

    public string TrackerProjectSlug { get; set; } = string.Empty;

    public string? TrackerActiveStates { get; set; }

    public string? TrackerTerminalStates { get; set; }

    public string? HookAfterCreate { get; set; }

    public string? HookBeforeRun { get; set; }

    public string? HookAfterRun { get; set; }

    public string? HookBeforeRemove { get; set; }

    public int HookTimeoutMs { get; set; } = 60000;

    public int MaxConcurrentAgents { get; set; } = 10;

    public int MaxRetryBackoffMs { get; set; } = 300000;

    public int MaxTurns { get; set; } = 1;

    public string? MaxConcurrentAgentsByState { get; set; }

    public string? CopilotProvider { get; set; }

    public string? CopilotBaseUrl { get; set; }

    public string? CopilotModelId { get; set; }

    public string? CopilotApiKey { get; set; }

    public string? CopilotWireApi { get; set; }

    public bool TelemetryContentEnabled { get; set; }

    public string? GitHubPersonalAccessToken { get; set; }

    public string? DefaultWorkflowName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}