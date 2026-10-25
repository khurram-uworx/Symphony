# GitHub Issues tracker integration (multi-user)

## Problem
Add GitHub Issues support (`tracker.kind: github`) in the current multi-user Symphony architecture. The tracker is used to resolve submitted ticket IDs into `Issue` objects used by `JobProcessor`/`AgentRunner`. Configuration is per-user and encrypted. Existing GitHub PAT can be reused.

## Proposed changes
1. Add `GitHubIssuesClient` implementing `IIssueTracker`.
2. Wire it into `IssueTrackerFactory`.
3. Add endpoint fallback and token fallback in config builders.
4. Update validation and UI.
5. Enable Copilot GitHub behavior for github tracker.
6. Update docs.
7. Add tests.

## Implementation plan
- [ ] Create and write docs/TODO.md
- [ ] Add GitHubIssuesClient.cs
- [ ] Update IssueTrackerFactory
- [ ] Update ServiceConfig.cs fallbacks
- [ ] Update ServiceConfigFactory.cs fallbacks
- [ ] Update ConfigurationService.cs fallback
- [ ] Update ConfigurationDto validation/UI
- [ ] Update CopilotAgent.cs conditional
- [ ] Update docs (CONFIG/README/ARCH)
- [ ] Add unit tests
- [ ] Build
- [ ] Run tests (ask first)
- [ ] Branch review
- [ ] Remove TODO.md

## GitHub issues log
- [ ] (none yet)

## Verification
- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`

## Blast radius
IssueTrackerFactory, config fallback code, validation, UI, CopilotAgent, docs/tests. No DB migration expected (tracker kind is string).
"