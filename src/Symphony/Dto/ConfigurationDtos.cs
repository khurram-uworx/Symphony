using Symphony.Data;
using Symphony.Security;

namespace Symphony.Dto;

public class ConfigurationDto
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? TrackerKind { get; set; }

    public string? TrackerEndpoint { get; set; }

    public string? TrackerApiKey { get; set; }

    public string? TrackerProjectSlug { get; set; }

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

    public string? DefaultWorkflowName { get; set; }

    public string? GitHubPersonalAccessToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static ConfigurationDto From(UserConfiguration source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ConfigurationDto
        {
            Id = source.Id,
            UserId = source.UserId,
            TrackerKind = source.TrackerKind,
            TrackerEndpoint = source.TrackerEndpoint,
            TrackerApiKey = string.IsNullOrEmpty(source.TrackerApiKey) ? string.Empty : Redaction.Redacted,
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
            CopilotApiKey = string.IsNullOrEmpty(source.CopilotApiKey) ? null : Redaction.Redacted,
            CopilotWireApi = source.CopilotWireApi,
            TelemetryContentEnabled = source.TelemetryContentEnabled,
            GitHubPersonalAccessToken = string.IsNullOrEmpty(source.GitHubPersonalAccessToken) ? null : Redaction.Redacted,
            DefaultWorkflowName = source.DefaultWorkflowName,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    public UserConfiguration ToUserConfiguration()
    {
        return new UserConfiguration
        {
            Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString() : Id,
            UserId = UserId,
            TrackerKind = TrackerKind ?? string.Empty,
            TrackerEndpoint = TrackerEndpoint ?? string.Empty,
            TrackerApiKey = TrackerApiKey ?? string.Empty,
            TrackerProjectSlug = TrackerProjectSlug ?? string.Empty,
            TrackerActiveStates = TrackerActiveStates,
            TrackerTerminalStates = TrackerTerminalStates,
            HookAfterCreate = HookAfterCreate,
            HookBeforeRun = HookBeforeRun,
            HookAfterRun = HookAfterRun,
            HookBeforeRemove = HookBeforeRemove,
            HookTimeoutMs = HookTimeoutMs,
            MaxConcurrentAgents = MaxConcurrentAgents,
            MaxRetryBackoffMs = MaxRetryBackoffMs,
            MaxTurns = MaxTurns,
            MaxConcurrentAgentsByState = MaxConcurrentAgentsByState,
            CopilotProvider = CopilotProvider,
            CopilotBaseUrl = CopilotBaseUrl,
            CopilotModelId = CopilotModelId,
            CopilotApiKey = CopilotApiKey,
            CopilotWireApi = CopilotWireApi,
            TelemetryContentEnabled = TelemetryContentEnabled,
            GitHubPersonalAccessToken = GitHubPersonalAccessToken,
            DefaultWorkflowName = DefaultWorkflowName,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    public bool IsValid(out string? error)
    {
        if (MaxConcurrentAgents <= 0)
        {
            error = "MaxConcurrentAgents must be greater than zero.";
            return false;
        }

        if (MaxRetryBackoffMs <= 0)
        {
            error = "MaxRetryBackoffMs must be greater than zero.";
            return false;
        }

        if (MaxTurns <= 0)
        {
            error = "MaxTurns must be greater than zero.";
            return false;
        }

        if (HookTimeoutMs <= 0)
        {
            error = "HookTimeoutMs must be greater than zero.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(TrackerKind))
        {
            var trackerKind = TrackerKind.Trim().ToLowerInvariant();
            if (trackerKind is not ("none" or "inline" or "null" or "linear" or "jira" or "github"))
            {
                error = "TrackerKind must be one of none, linear, jira, or github.";
                return false;
            }

            if (trackerKind == "github")
            {
                if (!string.IsNullOrWhiteSpace(TrackerEndpoint) &&
                    !Uri.TryCreate(TrackerEndpoint, UriKind.Absolute, out _))
                {
                    error = "TrackerEndpoint must be an absolute URL.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(TrackerProjectSlug))
                {
                    var parts = TrackerProjectSlug.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length != 2)
                    {
                        error = "TrackerProjectSlug must be in owner/repo form.";
                        return false;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(CopilotProvider))
        {
            var provider = CopilotProvider.Trim().ToLowerInvariant();
            if (provider is not ("openai" or "azure" or "anthropic"))
            {
                error = "CopilotProvider must be one of openai, azure, anthropic.";
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(CopilotBaseUrl) &&
            !Uri.TryCreate(CopilotBaseUrl, UriKind.Absolute, out _))
        {
            error = "CopilotBaseUrl must be an absolute URL.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool IsValid(ConfigurationDto dto, out string? error)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.IsValid(out error);
    }
}

public sealed class ConfigurationResponse : ConfigurationDto { }
public sealed class UserConfigurationDto : ConfigurationDto { }
public sealed class ConfigurationModel : ConfigurationDto { }
public sealed class ConfigurationRequest : ConfigurationDto { }
public sealed class UpdateConfigurationRequest : ConfigurationDto { }
public sealed class SaveConfigurationRequest : ConfigurationDto { }
public sealed class ConfigurationUpdateRequest : ConfigurationDto { }