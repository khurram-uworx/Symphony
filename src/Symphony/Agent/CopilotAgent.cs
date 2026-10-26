using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Symphony.Config;
using Symphony.Services;
using System.Text.Json;

namespace Symphony.Agent;

class CopilotAgent : ICodingAgent
{
    readonly ServiceConfig config;
    readonly ILogger<CopilotAgent> logger;
    readonly JobTelemetryCapture? telemetryCapture;
    CopilotClient? copilotClient;
    AIAgent? agent;

    public CopilotAgent(ServiceConfig config)
        : this(NullLogger<CopilotAgent>.Instance, config, null)
    {
    }

    public CopilotAgent(ILogger<CopilotAgent> logger, ServiceConfig config, JobTelemetryCapture? telemetryCapture = null)
    {
        this.logger = logger;
        this.config = config;
        this.telemetryCapture = telemetryCapture;
    }

    public async Task InitializeAsync(string workspacePath, LiveSession liveSession, Action<CodingAgentEvent> onEvent, CancellationToken cancellationToken)
    {
        var clientOptions = new CopilotClientOptions
        {
            WorkingDirectory = workspacePath
        };

        if (telemetryCapture?.CurrentFilePath is string telemetryPath)
        {
            clientOptions.Telemetry = new TelemetryConfig
            {
                FilePath = telemetryPath,
                ExporterType = "file",
                SourceName = "symphony",
                CaptureContent = telemetryCapture.CurrentContentEnabled,
            };
        }

        var repository = config.Repository;
        var usesGitHubTracker = string.Equals(config.Tracker.Kind, "github", StringComparison.OrdinalIgnoreCase);
        if (repository is not null || usesGitHubTracker)
        {
            var token = repository?.Token;
            if (string.IsNullOrWhiteSpace(token) && usesGitHubTracker)
                token = config.Tracker.ApiKey;

            clientOptions.Environment = Symphony.Repositories.GitHubEnvironment.Build(
                Symphony.Repositories.GitHubEnvironment.FromProcessEnvironment(),
                repository?.Slug ?? config.Tracker.ProjectSlug,
                token,
                repository?.Identity);
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