using Microsoft.Extensions.Logging;
using Symphony.Config;
using Symphony.Domain;
using Symphony.IssueTracker;
using Symphony.Orchestration;
using Symphony.Workflows;
using Symphony.Workspaces;

namespace Symphony.Agent;

public record AgentRunResult(bool Success, string? Reason, Exception? Exception, int TurnCount = 0)
{
    public static AgentRunResult CreateSuccess(int turnCount = 0) => new(true, null, null, turnCount);
    public static AgentRunResult Fail(string reason, Exception? ex = null, int turnCount = 0) => new(false, reason, ex, turnCount);
}

class AgentRunner
{
    readonly IServiceConfigProvider configProvider;
    readonly WorkspaceManager workspaceManager;
    readonly ICodingAgent agent;
    readonly IIssueTracker issueTracker;
    readonly ILogger<AgentRunner> logger;
    readonly PromptRenderer promptRenderer;

    public AgentRunner(
        IServiceConfigProvider configProvider,
        WorkspaceManager workspaceManager,
        ICodingAgent agent,
        IIssueTracker issueTracker,
        ILogger<AgentRunner> logger)
    {
        this.configProvider = configProvider;
        this.workspaceManager = workspaceManager;
        this.agent = agent;
        this.issueTracker = issueTracker;
        this.logger = logger;
        promptRenderer = new PromptRenderer();
    }

    public async Task<AgentRunResult> RunAttemptAsync(Issue issue, int? attempt, LiveSession liveSession, Action<CodingAgentEvent> onEvent, CancellationToken cancellationToken)
    {
        var config = configProvider.GetConfig();
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            { "issue_id", issue.Id },
            { "issue_identifier", issue.Identifier }
        });
        Workspace workspace;

        try
        {
            workspace = await workspaceManager.CreateForIssueAsync(issue.Identifier, cancellationToken);
        }
        catch (Exception ex)
        {
            return AgentRunResult.Fail("workspace error", ex);
        }

        if (!await workspaceManager.RunHookAsync(config.Hooks.BeforeRun, workspace.Path, config.Hooks.TimeoutMs, cancellationToken))
            return AgentRunResult.Fail("before_run hook error");

        try
        {
            await agent.InitializeAsync(workspace.Path, liveSession, onEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            await workspaceManager.RunHookBestEffortAsync(config.Hooks.AfterRun, workspace.Path, config.Hooks.TimeoutMs, cancellationToken);
            return AgentRunResult.Fail("agent session startup error", ex);
        }

        try
        {
            var maxTurns = config.Agent.MaxTurns;
            var turn = 1;

            while (true)
            {
                var prompt = string.IsNullOrWhiteSpace(config.Workflow.PromptTemplate)
                    ? "You are working on an issue from Linear."
                    : promptRenderer.Render(config.Workflow.PromptTemplate, issue, attempt);

                try
                {
                    _ = await agent.ExecuteTurnAsync(prompt, liveSession, cancellationToken);
                    liveSession.TurnCount++;
                }
                catch (CodingAgentException ex)
                {
                    return AgentRunResult.Fail(ex.Message, ex.InnerException ?? ex, liveSession.TurnCount);
                }

                var refreshed = await issueTracker.FetchIssueStatesByIdsAsync(new[] { issue.Id }, cancellationToken);
                if (refreshed.Count == 0)
                    break;

                issue = refreshed[0];
                if (!config.ActiveStates.Contains(issue.NormalizedState))
                    break;

                if (turn >= maxTurns)
                    break;

                turn++;
            }
        }
        catch (WorkflowException ex)
        {
            await workspaceManager.RunHookBestEffortAsync(config.Hooks.AfterRun, workspace.Path, config.Hooks.TimeoutMs, cancellationToken);
            return AgentRunResult.Fail("prompt error", ex, liveSession.TurnCount);
        }
        catch (Exception ex)
        {
            await workspaceManager.RunHookBestEffortAsync(config.Hooks.AfterRun, workspace.Path, config.Hooks.TimeoutMs, cancellationToken);
            return AgentRunResult.Fail("agent turn error", ex, liveSession.TurnCount);
        }
        finally
        {
            await agent.ShutdownAsync(cancellationToken);
        }

        await workspaceManager.RunHookBestEffortAsync(config.Hooks.AfterRun, workspace.Path, config.Hooks.TimeoutMs, cancellationToken);

        return AgentRunResult.CreateSuccess(liveSession.TurnCount);
    }
}
