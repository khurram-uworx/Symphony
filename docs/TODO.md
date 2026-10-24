# T24: Database-backed queue wake-up signal

Branch: `khurram/48`  
Issue: https://github.com/khurram-uworx/Symphony/issues/48

## Problem

Symphony persists queued work in the database, but the notification path currently uses two different
`JobQueue` instances: the static `defaultInstance` and the DI-created singleton. Enqueue paths call
both `JobQueue.NotifyNewJob()` and `jobQueue?.NotifyInstance()` to compensate, but a future
processor consuming a different instance could miss wake-ups, or wake two channels for one job.

The channel must remain advisory only: persistent truth is `Jobs.State = 'Queued'`. The existing
bounded channel (`1`, `DropWrite`) already coalesces bursts, which is correct for a wake-up.

## Proposed changes

1. Add `JobQueue.Default` exposing the static default instance.
2. Register that same instance in DI (`AddSingleton(JobQueue.Default)`) so there is exactly one channel.
3. Keep the public wake-up surface (`NotifyInstance`, `SignalInstanceAsync`,
   `WaitForSignalInstanceAsync`) unchanged so existing tests and callers compile.
4. Update `JobService` and `WorkEndpoints` so the best-effort notifications cannot throw and only
   trigger one signal per enqueue.
5. Add focused `JobQueue` unit tests in a new `tests/Symphony.Tests/Processing/JobQueueTests.cs`.
6. Format `JobQueue.cs` (trailing newline).

## Grounding

- Code-memory confirms callers:
  - `Program.cs` registers `JobQueue` singleton
  - `JobService` calls static and instance notify
  - `WorkEndpoints` calls static and instance notify
  - `Job` index `(State, QueuedAt)` exists for queue polling
- Microsoft Learn: `System.Threading.Channels.ChannelReader.ReadAsync(CancellationToken)` throws
  `OperationCanceledException` on cancellation; bounded channel writes return immediately.

## Blast radius

- `src/Symphony/Processing/JobQueue.cs` (public API unchanged; adds `Default`)
- `src/Symphony/Program.cs` (DI factory)
- `src/Symphony/Services/JobService.cs` (notification path)
- `src/Symphony/Api/WorkEndpoints.cs` (notification path)
- `tests/Symphony.Tests/Processing/JobQueueTests.cs` (new)

No database, migration, or public DTO change.

## Verification

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Existing 46 unit tests remain green.
- New tests verify notify-before-wait wake-up, cancellation propagation, burst coalescing, and
  no-throw signaling.

## Planned commits

1. `docs: plan T24 database-backed queue wake-up in TODO.md`
2. `feat: unify JobQueue static and DI instance`
3. `refactor: simplify enqueue notification best-effort paths`
4. `test: add JobQueue wake-up signal tests`
5. `docs: remove TODO.md — plan executed`

## GitHub issues log

- (none yet)
