# T30: Job issue and event persistence

## Problem
Persist per-issue outcomes and telemetry events during job processing. Today these writes live inside `JobProcessor` private helpers; T30 moves ownership into `JobService`, exposes them as public persistence methods, and keeps the processor as the orchestrator. Event appends must be redacted, best-effort, chronological, and never crash a run.

## Proposed changes

### `src/Symphony/Services/JobService.cs`
Add persistence methods (plus compatibility aliases):

- `CreateIssueRowAsync` / `CreateJobIssueAsync` / `CreateIssueAsync` / `AddIssueAsync`
  - Creates a `JobIssue` row with identifier, title, state, `Success=false`, `TurnCount=0`, timings and workspace path.
- `CreateFailedIssueRowAsync` / `CreateFailedJobIssueAsync` / `AddFailedIssueAsync`
  - Creates a failed `JobIssue` row.
- `UpdateIssueRowAsync` / `UpdateJobIssueAsync` / `UpdateIssueAsync`
  - Updates state, success, reason, completed time, workspace path and turn count.
- `MarkJobStateAsync` / `UpdateJobStateAsync` / `SetJobStateAsync`
  - Updates job state/error/summary and completion timestamp.
- `AppendEventAsync` / `AppendJobEventAsync` / `AddEventAsync` / `RecordEventAsync`
  - Redacts payload via `Redaction.RedactObject(payload)` and best-effort inserts a `JobEvent`.

### `src/Symphony/Processing/JobProcessor.cs`
Resolve scoped `JobService` and call its methods instead of private helpers. Remove duplicated write helpers. Keep cancellation/failure behavior.

### `src/Symphony/Agent/AgentRunner.cs`
Extend `AgentRunResult` with `TurnCount` (default 0) and carry `LiveSession.TurnCount` through results so `JobIssue.TurnCount` can be persisted.

### `src/Symphony/Security/Redaction.cs`
Keep secret-name coverage and payload sanitization; no behavioral change beyond already redacting PAT/API-key fields.

## Verification
- Build: `dotnet build Symphony.slnx`
- Tests: `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (with human confirmation before running)
- Manual Playwright check (local `dotnet run`) for job detail/events after PR.

## Planned commits
1. `docs: plan T30 in TODO.md`
2. `feat: add AgentRunResult turn count`
3. `feat: add JobService issue and event persistence methods`
4. `refactor: use JobService persistence in JobProcessor`
5. `test: cover JobService issue/event persistence and redaction`
6. `docs: remove TODO.md — plan executed`

## Blast radius
- Core job execution path: `JobProcessor`, `AgentRunner`.
- `JobService` public API additions; scoped DI registration already exists.
- `AgentRunResult` callers: `JobProcessor`, `OrchestratorService`.
- Tests: existing `JobQueueTests`, `JobPersistenceTests`; new JobService tests.

## GitHub issues log
- None yet.
