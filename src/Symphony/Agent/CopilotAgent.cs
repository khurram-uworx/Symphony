using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Symphony.Config;
using Symphony.Orchestration;
using System.Text.Json;

namespace Symphony.Agent;

class CopilotAgent : ICodingAgent
{
    readonly ServiceConfig config;
    readonly ILogger<CopilotAgent> logger;
    CopilotClient? copilotClient;
    AIAgent? agent;

    public CopilotAgent(ILogger<CopilotAgent> logger, ServiceConfig config)
    {
        this.logger = logger;
        this.config = config;
    }

    public async Task InitializeAsync(string workspacePath, LiveSession liveSession, Action<CodingAgentEvent> onEvent, CancellationToken cancellationToken)
    {
        copilotClient = new CopilotClient();
        await copilotClient.StartAsync();

        var sessionConfig = new SessionConfig
        {
#pragma warning disable GHCP001
            OnPermissionRequest = (req, inv) => Task.FromResult(PermissionDecision.ApproveOnce()),
#pragma warning restore GHCP001
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["linear"] = new McpStdioServerConfig
                {
                    Command = "npx",
                    Args = new List<string> { "-y", "mcp-remote", "https://mcp.linear.app/mcp" },
                    Tools = new List<string> { "*" },
                },
            },
            WorkingDirectory = workspacePath
        };
        sessionConfig.OnEvent = evt =>
        {
            onEvent(new CodingAgentEvent(evt.Type, null, evt.Timestamp, new JsonElement()));
            this.logger.LogInformation("[Copilot] {Type} {Id}", evt.Type, evt.Id);
        };

        agent = copilotClient.AsAIAgent(sessionConfig);
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
