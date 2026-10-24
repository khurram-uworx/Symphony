using Microsoft.Extensions.Logging.Abstractions;
using Symphony.Data;
using Symphony.Workflows;
using Symphony.Workspaces;
using System.Text.Json;

namespace Symphony.Config;

public class ServiceConfigFactory
{
    public static ServiceConfig Build(
        object? configuration,
        string workflowContent,
        IEnumerable<string>? submittedStates = null)
    {
        var workflow = new WorkflowLoader(NullLogger<WorkflowLoader>.Instance).Parse(workflowContent ?? string.Empty);
        return Build(configuration, workflow, submittedStates);
    }

    public static ServiceConfig Build(
        object? configuration,
        WorkflowDefinition workflow,
        IEnumerable<string>? submittedStates = null)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var dictionary = AsDictionary(configuration);
        UserConfiguration? user = configuration as UserConfiguration;

        TrackerConfig tracker;
        WorkspaceConfig workspace;
        HooksConfig hooks;
        AgentConfig agent;
        CodexConfig? codex;
        CopilotConfig? copilot;

        if (dictionary is not null)
        {
            tracker = TrackerConfig.From(dictionary);
            workspace = WorkspaceConfig.From(dictionary);
            hooks = HooksConfig.From(dictionary);
            agent = AgentConfig.From(dictionary);
            codex = CodexConfig.From(dictionary);
            copilot = CopilotConfig.From(dictionary);
        }
        else
        {
            user ??= new UserConfiguration();
            tracker = BuildTracker(user);
            workspace = BuildWorkspace(user);
            hooks = BuildHooks(user);
            agent = BuildAgent(user);
            codex = null;
            copilot = BuildCopilot(user);
        }

        var activeStates = new HashSet<string>(tracker.ActiveStates, StringComparer.OrdinalIgnoreCase);
        foreach (var state in submittedStates ?? [])
        {
            if (!string.IsNullOrWhiteSpace(state))
                activeStates.Add(state.Trim());
        }

        tracker = new TrackerConfig(
            tracker.Kind,
            tracker.Endpoint,
            tracker.ApiKey,
            tracker.ProjectSlug,
            activeStates,
            tracker.TerminalStates);

        workspace = ApplyWorkspaceOverride(workspace, workflow);
        hooks = ApplyHooksOverride(hooks, workflow);
        agent = ApplyAgentOverrides(agent, workflow);

        return new ServiceConfig(workflow, tracker, workspace, hooks, agent, codex, copilot);
    }

    static TrackerConfig BuildTracker(UserConfiguration user)
    {
        var kind = string.IsNullOrWhiteSpace(user.TrackerKind) ? TrackerConfig.None : user.TrackerKind;
        var endpoint = user.TrackerEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint) && string.Equals(kind, "linear", StringComparison.OrdinalIgnoreCase))
            endpoint = "https://api.linear.app/graphql";

        var active = SplitStates(user.TrackerActiveStates, new[] { "Todo", "In Progress" });
        var terminal = SplitStates(user.TrackerTerminalStates, new[] { "Closed", "Cancelled", "Canceled", "Duplicate", "Done" });

        return new TrackerConfig(
            kind,
            endpoint ?? string.Empty,
            user.TrackerApiKey ?? string.Empty,
            user.TrackerProjectSlug ?? string.Empty,
            active,
            terminal);
    }

    static WorkspaceConfig BuildWorkspace(UserConfiguration user)
    {
        var rawRoot = string.IsNullOrWhiteSpace(user.WorkspaceRoot)
            ? Path.Combine(Path.GetTempPath(), "symphony_workspaces")
            : user.WorkspaceRoot!;
        return new WorkspaceConfig(WorkspacePath.RequireAbsoluteRoot(ConfigReader.ResolvePath(rawRoot)));
    }

    static HooksConfig BuildHooks(UserConfiguration user)
    {
        var timeout = user.HookTimeoutMs;
        if (timeout <= 0)
            timeout = 60000;

        return new HooksConfig(
            user.HookAfterCreate,
            user.HookBeforeRun,
            user.HookAfterRun,
            user.HookBeforeRemove,
            timeout);
    }

    static AgentConfig BuildAgent(UserConfiguration user)
    {
        var max = user.MaxConcurrentAgents;
        if (max <= 0)
            max = 10;

        var backoff = user.MaxRetryBackoffMs;
        if (backoff <= 0)
            backoff = 300000;

        var maxTurns = user.MaxTurns;
        if (maxTurns <= 0)
            maxTurns = 1;

        var perState = ParsePerState(user.MaxConcurrentAgentsByState);

        return new AgentConfig(max, backoff, maxTurns, perState);
    }

    static CopilotConfig? BuildCopilot(UserConfiguration user)
    {
        if (string.IsNullOrWhiteSpace(user.CopilotProvider))
            return null;

        return new CopilotConfig(
            user.CopilotProvider.Trim(),
            user.CopilotBaseUrl,
            user.CopilotModelId,
            user.CopilotApiKey,
            user.CopilotWireApi);
    }

    static HashSet<string> SplitStates(string? raw, IEnumerable<string> fallback)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var part in raw!.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                    result.Add(part.Trim());
            }
        }

        if (result.Count == 0)
        {
            foreach (var state in fallback)
            {
                if (!string.IsNullOrWhiteSpace(state))
                    result.Add(state.Trim());
            }
        }

        return result;
    }

    static IReadOnlyDictionary<string, int> ParsePerState(string? raw)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var text = raw!.Trim();

        if (text.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var intValue) && intValue > 0)
                        result[property.Name.Trim().ToLowerInvariant()] = intValue;
                    else if (property.Value.ValueKind == JsonValueKind.String && int.TryParse(property.Value.GetString(), out var parsed) && parsed > 0)
                        result[property.Name.Trim().ToLowerInvariant()] = parsed;
                }

                if (result.Count > 0)
                    return result;
            }
            catch
            {
                // Fall through to simple parsing.
            }
        }

        foreach (var part in text.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = part.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            string key;
            string valueText;
            var separator = normalized.IndexOfAny(new[] { ':', '=' });
            if (separator >= 0)
            {
                key = normalized.Substring(0, separator).Trim().Trim('"');
                valueText = normalized.Substring(separator + 1).Trim().Trim('"');
            }
            else
            {
                continue;
            }

            if (int.TryParse(valueText, out var value) && value > 0 && !string.IsNullOrWhiteSpace(key))
                result[key.ToLowerInvariant()] = value;
        }

        return result;
    }

    static IReadOnlyDictionary<string, object>? AsDictionary(object? configuration)
    {
        if (configuration is IReadOnlyDictionary<string, object> readOnly)
            return readOnly;

        if (configuration is IDictionary<string, object> typed)
            return new Dictionary<string, object>(typed, StringComparer.OrdinalIgnoreCase);

        if (configuration is System.Collections.IDictionary legacy)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in legacy)
            {
                if (entry.Key is not null)
                    result[entry.Key.ToString() ?? string.Empty] = entry.Value ?? string.Empty;
            }

            return result;
        }

        return null;
    }

    static WorkspaceConfig ApplyWorkspaceOverride(WorkspaceConfig workspace, WorkflowDefinition workflow)
    {
        var workspaceMap = ConfigReader.ReadMap(workflow.Config, "workspace");
        var root = workspaceMap.Count > 0
            ? ConfigReader.ReadString(workspaceMap, "root")
            : null;

        if (string.IsNullOrWhiteSpace(root))
            root = ConfigReader.ReadString(workflow.Config, "workspace_root");

        if (string.IsNullOrWhiteSpace(root))
            return workspace;

        return new WorkspaceConfig(WorkspacePath.RequireAbsoluteRoot(ConfigReader.ResolvePath(root!)));
    }

    static HooksConfig ApplyHooksOverride(HooksConfig hooks, WorkflowDefinition workflow)
    {
        var hooksMap = ConfigReader.ReadMap(workflow.Config, "hooks");
        if (hooksMap.Count == 0)
            return hooks;

        var result = hooks;
        if (hooksMap.TryGetValue("after_create", out _))
            result = result with { AfterCreate = ConfigReader.ReadString(hooksMap, "after_create") };
        if (hooksMap.TryGetValue("before_run", out _))
            result = result with { BeforeRun = ConfigReader.ReadString(hooksMap, "before_run") };
        if (hooksMap.TryGetValue("after_run", out _))
            result = result with { AfterRun = ConfigReader.ReadString(hooksMap, "after_run") };
        if (hooksMap.TryGetValue("before_remove", out _))
            result = result with { BeforeRemove = ConfigReader.ReadString(hooksMap, "before_remove") };
        if (hooksMap.TryGetValue("timeout_ms", out _))
        {
            var timeout = ConfigReader.ReadInt(hooksMap, "timeout_ms");
            if (timeout.HasValue && timeout.Value > 0)
                result = result with { TimeoutMs = timeout.Value };
        }

        return result;
    }

    static AgentConfig ApplyAgentOverrides(AgentConfig agent, WorkflowDefinition workflow)
    {
        var config = workflow.Config;
        var agentMap = ConfigReader.ReadMap(config, "agent");

        var result = agent;

        int? maxTurns = null;
        if (agentMap.TryGetValue("max_turns", out _))
            maxTurns = ConfigReader.ReadInt(agentMap, "max_turns");
        else if (config.TryGetValue("max_turns", out _))
            maxTurns = ConfigReader.ReadInt(config, "max_turns");

        if (maxTurns.HasValue && maxTurns.Value > 0)
            result = result with { MaxTurns = maxTurns.Value };

        IReadOnlyDictionary<string, object>? perStateSource = null;
        if (agentMap.TryGetValue("max_concurrent_agents_by_state", out var byStateRaw) &&
            byStateRaw is IReadOnlyDictionary<string, object> byStateMap)
        {
            perStateSource = byStateMap;
        }
        else if (config.TryGetValue("max_concurrent_agents_by_state", out var topLevelRaw) &&
                 topLevelRaw is IReadOnlyDictionary<string, object> topLevelMap)
        {
            perStateSource = topLevelMap;
        }

        if (perStateSource is not null)
        {
            var merged = new Dictionary<string, int>(result.MaxConcurrentByState, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in perStateSource)
            {
                var normalizedKey = key.Trim().ToLowerInvariant();
                if (value is int intValue && intValue > 0)
                    merged[normalizedKey] = intValue;
                else if (value is long longValue && longValue > 0)
                    merged[normalizedKey] = (int)longValue;
                else if (value is string stringValue && int.TryParse(stringValue, out var parsed) && parsed > 0)
                    merged[normalizedKey] = parsed;
            }

            result = result with { MaxConcurrentByState = merged };
        }

        return result;
    }
}