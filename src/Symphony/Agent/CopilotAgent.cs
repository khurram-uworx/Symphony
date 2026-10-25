using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Symphony.Config;
using System.Text.Json;

namespace Symphony.Agent;

class CopilotAgent : ICodingAgent
{
    readonly ServiceConfig config;
    readonly ILogger<CopilotAgent> logger;
    CopilotClient? copilotClient;
    AIAgent? agent;

    public CopilotAgent(ServiceConfig config)
        : this(NullLogger<CopilotAgent>.Instance, config)
    {
    }

    public CopilotAgent(ILogger<CopilotAgent> logger, ServiceConfig config)
    {
        this.logger = logger;
        this.config = config;
    }

    public async Task InitializeAsync(string workspacePath, LiveSession liveSession, Action<CodingAgentEvent> onEvent, CancellationToken cancellationToken)
    {
        var clientOptions = new CopilotClientOptions
        {
            WorkingDirectory = workspacePath
        };

        if (string.Equals(config.Tracker.Kind, "github", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(config.Tracker.ApiKey))
        {
            var token = config.Tracker.ApiKey;
            var environment = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .Where(entry => entry.Key is string)
                .ToDictionary(
                    entry => (string)entry.Key,
                    entry => entry.Value?.ToString() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            environment["GH_TOKEN"] = token;
            environment["GITHUB_TOKEN"] = token;
            environment["GIT_TERMINAL_PROMPT"] = "0";
            if (!string.IsNullOrWhiteSpace(config.Tracker.ProjectSlug))
                environment["GITHUB_REPOSITORY"] = config.Tracker.ProjectSlug;

            clientOptions.Environment = environment;
        }

        copilotClient = new CopilotClient(clientOptions);
        await copilotClient.StartAsync();

        var sessionConfig = new SessionConfig
        {
#pragma warning disable GHCP001
            OnPermissionRequest = (req, inv) => Task.FromResult(PermissionDecision.ApproveOnce()),
#pragma warning restore GHCP001
            McpServers = new Dictionary<string, McpServerConfig>(),
            WorkingDirectory = workspacePath
        };

        if (string.Equals(config.Tracker.Kind, "linear", StringComparison.OrdinalIgnoreCase))
        {
            sessionConfig.McpServers["linear"] = new McpStdioServerConfig
            {
                Command = "npx",
                Args = ["-y", "mcp-remote", "https://mcp.linear.app/mcp"],
                Tools = ["*"],
            };
        }

        var copilotConfig = config.Copilot;
        if (copilotConfig is not null)
        {
            if (!string.IsNullOrWhiteSpace(copilotConfig.ModelId))
                sessionConfig.Model ??= copilotConfig.ModelId;

            var providerType = copilotConfig.Provider?.Trim();
            if (!string.IsNullOrWhiteSpace(providerType))
            {
                sessionConfig.Provider = new GitHub.Copilot.ProviderConfig
                {
                    Type = providerType.ToLowerInvariant(),
                    BaseUrl = string.IsNullOrWhiteSpace(copilotConfig.BaseUrl)
                        ? null
                        : copilotConfig.BaseUrl.TrimEnd('/') + "/",
                    ApiKey = copilotConfig.ApiKey,
                    WireApi = copilotConfig.WireApi,
                    ModelId = copilotConfig.ModelId,
                    WireModel = copilotConfig.ModelId
                };
            }
        }

        sessionConfig.OnEvent = evt =>
        {
            onEvent(new CodingAgentEvent(evt.Type, null, evt.Timestamp, new JsonElement()));
            this.logger.LogInformation("[Copilot] {Type} {Id}", evt.Type, evt.Id);
        };

        agent = copilotClient.AsAIAgent(sessionConfig);
        onEvent(new CodingAgentEvent("agent.initialized", JsonSerializer.Serialize(new { provider = copilotConfig?.Provider ?? copilotConfig?.Type ?? "copilot", model = copilotConfig?.ModelId ?? sessionConfig.Model }), DateTimeOffset.UtcNow, new JsonElement()));
        liveSession.SessionId = Guid.NewGuid().ToString();
    }

    public async Task<string?> ExecuteTurnAsync(string prompt, LiveSession liveSession, CancellationToken cancellationToken)
    {
        if (agent == null)
            throw new InvalidOperationException("Agent not initialized. Call InitializeAsync first.");

        try
        {
            var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
            liveSession.TurnCount++;
            return response.Text;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (InvalidOperationException ex)
        {
            //Session error: 402 You have no quota
            if (ex.Message.Contains("You have no quota"))
                throw new CodingAgentException("No remaining quota", ex);

            throw;
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (copilotClient != null)
            await copilotClient.DisposeAsync();
    }
}