# Plan: T20 ServiceConfigFactory

GitHub issue: #44

## Problem
Create `ServiceConfigFactory.Build(...)` so a job can build runtime `ServiceConfig` from saved user configuration, selected workflow content, and submitted work state. Tracker/config credentials must remain immutable from workflow front matter; allowed overrides are workspace root, hooks, max turns, and per-state concurrency. Codex front matter is ignored.

## Decision
Allowed front-matter override keys:
- `workspace.root`
- `hooks.*`
- `agent.max_turns` (or top-level `max_turns`)
- `agent.max_concurrent_agents_by_state` (or top-level equivalent)
- Tracker sections are never applied.
- Codex sections are ignored.

## Proposed changes
1. Reshape `ServiceConfig`:
   - drop `PollingConfig`
   - add `CopilotConfig?`
   - retain `CodexConfig?`
2. Add `CopilotConfig` record.
3. Add `WorkflowLoader.Parse(string content)` and reuse it.
4. Convert both `UserConfiguration` and config dictionaries into config records.
5. Implement `ServiceConfigFactory.Build`.
   - build tracker/workspace/hooks/agent/copilot from user config
   - parse workflow content into `WorkflowDefinition`
   - merge submitted item states into active states
   - apply allowed front-matter overrides
6. Update `ServiceConfigProvider.GetConfig` and `OrchestratorService` for the new record.

## Verification
- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`

## Planned commits
1. docs: plan T20 in TODO.md
2. feat: reshape ServiceConfig and add CopilotConfig
3. feat: parse workflow content in WorkflowLoader
4. feat: add ServiceConfigFactory
5. fix: update runtime callers for new ServiceConfig shape
6. test/cleanup as needed

## GitHub issues log
None currently.