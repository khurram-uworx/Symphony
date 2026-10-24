# Make the work tracker optional and remove candidate polling

## Problem

Symphony is now a push-based web product: work arrives through `POST /api/work`, is persisted as a `Job`, and is executed by `JobProcessor`. The old polling model still shapes the code even though it is not shipped:

- `Orchestration/OrchestratorService` is never registered as a hosted service, but its `ValidateForDispatch` path is the only place that hard-requires `tracker.kind`, `tracker.api_key`, and `tracker.project_slug`.
- `IssueTrackerFactory.Create` maps an empty tracker kind to `NullIssueTracker`, and `NullIssueTracker.FetchIssueStatesByIdsAsync` fabricates an `Issue` ("Inline work item", state `In Progress`) for any unknown ID. A submission with `ticketIds` and no tracker therefore *looks* resolved and runs against fake work.
- `AgentRunner` uses tracker state as its continuation signal. With a null tracker, inline work effectively runs one turn by accident, not by design.
- The configuration UI presents the tracker as the headline setting with no "none" option, and the docs still describe tracker values as required.
- `IIssueTracker` still exposes `FetchCandidateIssuesAsync` and `FetchTerminalIssuesAsync`, which only the deleted polling path used.

The tracker should become an optional context source: inline `workItems` need no tracker; `ticketIds` require one.

## Decisions (confirmed with the human)

1. No tracker configured, inline work loops up to `Agent.MaxTurns` without a state refresh. Default `MaxTurns` is 1.
2. A submission with **only** ticket IDs and no tracker is rejected with `400`. A mixed submission queues, runs its inline items, and records failed `JobIssue` rows for the ticket IDs.
3. `FetchCandidateIssuesAsync` and `FetchTerminalIssuesAsync` are removed from `IIssueTracker` and its implementations.
4. GitHub remains a repo/code host only in this change. GitHub repo/PAT requirements are Milestone 5 of `docs/plans/SYMPHONY-WEB.md`.
5. Keep the "tracker" naming (`TrackerKind`, `TrackerConfig`, stored columns). Only UI copy and docs reframe it as optional.

## Proposed changes

### 1. Remove the polling path and dead tracker surface

- Delete `src/Symphony/Orchestration/OrchestratorService.cs`.
- Move `LiveSession` from `src/Symphony/Orchestration/Models.cs` to `src/Symphony/Agent/LiveSession.cs` (namespace `Symphony.Agent`), then delete `RunningEntry`, `RetryEntry`, `OrchestratorState`, `CodexTotals`, and `CodexRateLimits`.
- Update `using` directives in `Agent/AgentRunner.cs`, `Agent/ICodingAgent.cs`, `Agent/CopilotAgent.cs`, `Agent/Codex/*`, and `Processing/JobProcessor.cs`.
- Delete `src/Symphony/Workflows/WorkflowManager.cs` and `src/Symphony/WorkflowPathResolver.cs`.
- In `src/Symphony/Config/ServiceConfigProvider.cs`, delete `ServiceConfigProvider`, `ConfigValidationResult`, and `PollingConfig`. Keep `ServiceConfig`, `TrackerConfig`, `WorkspaceConfig`, `HooksConfig`, `AgentConfig`, `CodexConfig`, and `ConfigReader`. Rename the file to `ServiceConfig.cs`.
- `src/Symphony/IssueTracker/IIssueTracker.cs`: keep only `FetchIssueStatesByIdsAsync`.
- Remove candidate/terminal implementations from `LinearClient`, `JiraClient`, and `NullIssueTracker`.
- `src/Symphony/IssueTracker/IssueTrackerFactory.cs`: collapse to `Create(ServiceConfig)`; delete the `submissionContext` overload, `IsInlineOnly`, and the reflection helpers.
- Update `tests/Symphony.Tests/NullIssueTrackerTests.cs` to drop the candidate/terminal tests.

### 2. Make "none" a first-class tracker kind

- `src/Symphony/Config/ServiceConfig.cs` (`TrackerConfig`): add `public const string None = "none"` and `IsNone`, which is true for empty, `none`, `inline`, and `null`.
- `TrackerConfig.From` and `ServiceConfigFactory.BuildTracker`: normalize a blank kind to `none`.
- `IssueTrackerFactory.Create`: `none`/`inline`/`null`/empty → `NullIssueTracker`; `linear` → `LinearClient`; `jira` → `JiraClient`; anything else throws listing the supported values.
- `src/Symphony/Dto/ConfigurationDtos.cs`: validate `TrackerKind` as empty/none/inline/null/linear/jira. Keep the existing masked-secret preservation in `AccountController.Configuration`.
- Tests: `tests/Symphony.Tests/Config/TrackerConfigTests.cs`, `ServiceConfigFactoryTests.cs`, `ConfigurationDtoTests.cs`.

### 3. Stop fabricating issues for unresolved ticket IDs

- `src/Symphony/IssueTracker/NullIssueTracker.cs`: return only seeded issues that match; omit unknown IDs. Collapse the overload set to the single `IReadOnlyList<string>` method.
- `src/Symphony/Processing/JobProcessor.cs`: when `ticketIds` are present and `config.Tracker.IsNone`, do not call the tracker. Write a failed `JobIssue` per ticket with the reason "No tracker configured; submit work items or configure a tracker." Inline items still run.
- Tests: unknown IDs omitted, seeded IDs returned unchanged in `NullIssueTrackerTests`.

### 4. Reject ticket-only work without a tracker

- `src/Symphony/Services/JobService.cs`: in `CreateCoreAsync`, after request validation, load the owner's `UserConfiguration`. If `TicketIds.Count > 0`, `WorkItems.Count == 0`, and the configured kind is none/blank, throw `ArgumentException` with a clear message so `WorkEndpoints` returns `400`.
- Mixed submissions are not rejected.
- Tests: `tests/Symphony.Tests/Data/JobServicePersistenceTests.cs` gains ticket-only rejection and mixed-acceptance coverage using the existing SQLite fixture.

### 5. Decouple the inline turn loop from tracker state

- `src/Symphony/Agent/AgentRunner.cs`: when `config.Tracker.IsNone`, skip `FetchIssueStatesByIdsAsync` and continue while `turn < MaxTurns`. Tracker-backed runs keep the existing refresh-and-continue behavior.
- No dedicated test: this is exercised through job execution; the rule is documented in the code path and covered indirectly by the manual smoke run.

### 6. UI: work source with a none option

- `src/Symphony/Views/Account/Configuration.cshtml`: retitle the section "Work source"; add `None (inline work only)`, keep `Linear` and `Jira`; hide endpoint/key/slug/state fields when none using the existing provider-toggle JS pattern; add copy explaining the tracker is optional.
- `src/Symphony/Views/Account/Run.cshtml`: add a hint that ticket IDs require a configured tracker.

### 7. Documentation

- `docs/CONFIG.md`: reframe around workflow front matter plus an optional tracker context source; remove polling and `ValidateForDispatch` "required" language.
- `docs/ARCHITECTURE.md`: job processing is push-only; the tracker is an optional context/state source.
- `docs/FEATURES.md`: trackers are optional context sources.

## Blast radius

- **Execution path:** `JobProcessor`, `AgentRunner`, `NullIssueTracker`, `IssueTrackerFactory`, `JobService`. Downstream: `WorkEndpoints`, `Run.cshtml`, job status DTOs.
- **Deletion:** `OrchestratorService`, `WorkflowManager`, `WorkflowPathResolver`, `ServiceConfigProvider`, orchestrator state types. `LiveSession` is used by every coding agent, so it must move before the orchestrator folder is removed.
- **Config model:** `TrackerConfig` is consumed by `ServiceConfigFactory`, `LinearClient`, `JiraClient`, `WorkspaceManager` (indirectly), and `AgentRunner`. Keep the record shape stable.
- **Persistence:** no schema or migration change. Existing empty `TrackerKind` rows are read as `none`.
- **Tests:** `NullIssueTrackerTests` changes exit criteria; new config/validation/job-service tests are additive.
- **Not touched:** Identity, database providers, job migration history, GitHub request validation (Milestone 5), Codex runtime source (retained, unregistered).

## Verification

1. `dotnet build Symphony.slnx` after each commit.
2. `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` after human approval.
3. Manual smoke: launch `dotnet run --project src/Symphony`, configure no tracker, submit inline work through `/account/run`, confirm the job queues without a tracker-related error; then submit only ticket IDs and confirm a `400`.

## Planned commits

1. `docs: plan optional tracker in TODO.md`
2. `Remove candidate polling and dead tracker fetches`
3. `Make none a first-class tracker kind`
4. `Stop fabricating issues for unresolved ticket IDs`
5. `Reject ticket-only work when no tracker is configured`
6. `Decouple inline turn loop from tracker state`
7. `Add work source UI with a none option`
8. `Document optional tracker configuration`
9. `docs: remove TODO.md — plan executed`

## GitHub issues log

- [ ] None yet. Create an issue immediately when deferred work or a concern is found during execution, and record it here.
