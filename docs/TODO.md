# T31: Tracker-based ticket resolution

## Problem

When a job is submitted with `ticketIds`, `JobProcessor` resolves them through the owner's configured tracker, but partial/missing ticket failures are only recorded as failed `JobIssue` rows. The job-level success/failure accounting currently starts from success (`allSuccess = true`, `anyFailure = false`) and ignores those failed rows. As a result:

- a job where every ticket is missing still reports `Completed` with summary `No issues.` even though failed issue rows exist;
- mixed runs can report success even though some requested tickets never ran;
- partial failure is not reflected in the final job state.

## Proposed changes

### `src/Symphony/Processing/JobProcessor.cs`

Track the number of failed ticket rows while resolving IDs.

Carry that count into job-level accounting.

Only treat the job as having "no issues" when nothing failed and nothing resolved.

Remove the unused `foundIds` set.

No change to tracker clients or factory behavior: Linear and Jira already accept human-readable keys through their id-based fetch APIs. Exceptions during fetch continue to surface every requested ID as a failed row through the existing resolution loop.

## Verification

- Build: `dotnet build Symphony.slnx`
- Tests: `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (with human confirmation before running)
- Inspect resulting `JobIssue` rows for missing tickets: `Success=false`, `State=Failed`, reason `Ticket not found.`

## Planned commits

1. `docs: plan T31 in TODO.md`
2. `feat: reflect partial ticket failures in job result`
3. `docs: remove TODO.md — plan executed`

## Blast radius

- Core job execution path: `JobProcessor.ProcessJobAsync`.
- Persistence unchanged: `JobService.CreateFailedIssueRowAsync`.
- No public API or schema change.
- Downstream callers of job state filtering by `CompletedWithFailures` are unaffected.

## GitHub issues log

- None yet.
