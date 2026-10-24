# T26: Issue tracker factory and NullIssueTracker

## Problem
Symphony needs request-aware selection of an issue tracker: Linear/Jira clients when configured, or
`NullIssueTracker` for inline-only work. Inline work must still support state refresh semantics without
external updates, and candidate polling must not be part of normal job processing.

## Proposed changes

### NullIssueTracker
File: `src/Symphony/IssueTracker/NullIssueTracker.cs`
- Implements `IIssueTracker`.
- `FetchCandidateIssuesAsync` returns empty.
- `FetchTerminalIssuesAsync` returns empty.
- `FetchIssueStatesByIdsAsync(IReadOnlyList<string> ids, ...)` returns one issue per id, preserving
  id and keeping a runnable active state.
- Issue-aware overloads accept `IEnumerable<Issue>` / `IReadOnlyList<Issue>` and return the supplied
  issues unchanged.
- Constructor overloads: parameterless and issue-seeded (`IEnumerable<Issue>`, `params Issue[]`).

### IssueTrackerFactory
File: `src/Symphony/IssueTracker/IssueTrackerFactory.cs`
- `Create(ServiceConfig)` and `Create(ServiceConfig, object?)`.
- Inline-only detection based on known submission context shapes (`WorkRequest`, `SubmissionContext`):
  when no ticket ids are present (or explicit inline items only) returns `NullIssueTracker`.
- Otherwise selects Linear/Jira client based on `ServiceConfig.Tracker.Kind`.
- Uses null loggers and an internal `IHttpClientFactory` adapter for Linear.

### SubmissionContext
File: `src/Symphony/IssueTracker/SubmissionContext.cs`
- Simple carrier with `TicketIds` and `WorkItems` lists.

## Verification
- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (with human approval)

## Planned commits
1. docs: plan T26 in TODO.md
2. feat: add NullIssueTracker
3. feat: add IssueTrackerFactory and SubmissionContext
4. test: add NullIssueTrackerTests

## Blast radius
- `src/Symphony/IssueTracker/*` only; `IIssueTracker` contract unchanged.
- `InternalsVisibleTo` lets tests access internal members.
- No DI registration changes in this task.

## GitHub issues log

- (none yet)
