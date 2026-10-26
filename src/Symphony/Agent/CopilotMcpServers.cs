using GitHub.Copilot;
using Symphony.Config;

namespace Symphony.Agent;

static class CopilotMcpServers
{
    internal const string LinearServerName = "linear";
    internal const string LinearMcpUrl = "https://mcp.linear.app/mcp";

    internal static Dictionary<string, McpServerConfig> Build(ServiceConfig config)
    {
        var servers = new Dictionary<string, McpServerConfig>();
        if (!IsLinear(config.Tracker) || string.IsNullOrWhiteSpace(config.Tracker.ApiKey))
            return servers;

        servers[LinearServerName] = new McpHttpServerConfig
        {
            Url = LinearMcpUrl,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = BuildAuthorizationHeader(config.Tracker.ApiKey)
            },
            Tools = ["*"]
        };

        return servers;
    }

    internal static string BuildAuthorizationHeader(string apiKey)
    {
        var token = apiKey.Trim();
        return token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? token : "Bearer " + token;
    }

    internal static string? Validate(ServiceConfig config, bool requiresLinearTracker)
    {
        if (!requiresLinearTracker || !IsLinear(config.Tracker))
            return null;

        return string.IsNullOrWhiteSpace(config.Tracker.ApiKey)
            ? "linear_credentials_missing: set the Linear API key in configuration or provide LINEAR_API_KEY."
            : null;
    }

    static bool IsLinear(TrackerConfig tracker) =>
        string.Equals(tracker.Kind, "linear", StringComparison.OrdinalIgnoreCase);
}
