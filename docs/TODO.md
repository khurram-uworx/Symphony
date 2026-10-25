# Plan: Job telemetry capture (Copilot SDK traces/metrics)

## Problem
Users and admins cannot see how a job actually executed. Symphony records coarse `JobEvent` rows only. The Copilot SDK emits detailed OpenTelemetry spans/metrics (session timing, tool calls, model calls, token usage), which we want to capture in-process and attach to jobs, with per-user content control and live-ish visibility while jobs run.

Decisions (from human):
1. Content capture is user-controlled (off by default, user can opt in).
2. Capture must support viewing an active job's progress (live polling; terminate possible).
3. Retention: keep telemetry for the most recent 20 jobs per user (matches job list), flush older runs.
4. Users see their own job telemetry (including content if they enabled it); admins see any user's telemetry subject to that user's content setting.
5. Codex telemetry is out of scope (follow-up issue when Codex is activated).
6. Persistence uses a normalized `JobTelemetry` table rather than a JSON column.

## Proposed changes

### Data / persistence
- `src/Symphony.Data/JobTelemetry.cs`: new entity `Id, JobId, Timestamp, Signal, TraceId, SpanId, ParentSpanId, Name, Severity, DurationMs, PayloadJson`, navigation to Job.
- `SymphonyDbContext`: `DbSet<JobTelemetry>`; configure key, index `(JobId, Timestamp)`, cascade delete from Job.
- `UserConfiguration`: add `TelemetryContentEnabled` bool (default false).
- Migrations: add `JobTelemetry` table and `TelemetryContentEnabled` column across SQLite, SQL Server, PostgreSQL, and update model snapshots via EF CLI.
- `src/Symphony/Dto/ConfigurationDtos.cs`: add `TelemetryContentEnabled` to `ConfigurationDto` and round-trip in `ConfigurationService`.
- `src/Symphony/Views/Account/Index.cshtml`: checkbox for content capture in Copilot settings.
- Retention helper: after persisting a job's telemetry, delete telemetry rows for jobs owned by the same user that are older than the most recent 20 jobs (by QueuedAt) that have telemetry.

### Capture service
- `src/Symphony/Services/JobTelemetryCapture.cs`:
  - Maintains `ConcurrentDictionary<Guid,string>` of job->temp JSONL path.
  - `BeginJob(Guid jobId, bool captureContent)`: sets current job `AsyncLocal`, creates temp path under `{TempPath}/symphony-telemetry/{jobId}.jsonl`, deletes stale file.
  - `CurrentFilePath` and `CaptureContent` for `CopilotAgent`.
  - `ReadLiveAsync(Guid jobId)`: parses JSONL if present, returns `JobTelemetryBundle` (spans/metrics/logs normalized).
  - `FinalizeAsync(Guid jobId, CancellationToken)`: parse file, persist `JobTelemetry` rows, delete temp file, apply retention.
- `CopilotAgent`: inject `JobTelemetryCapture`. In `InitializeAsync`, set `capture.BeginJob(...)`? Actually JobProcessor owns begin/finalize. CopilotAgent reads `capture.CurrentFilePath` and configures `Telemetry.FilePath` + `CaptureContent`.
- `JobProcessor.ProcessJobAsync`: call `telemetryCapture.BeginJob(job.Id, userConfig.TelemetryContentEnabled ?? false)` before agent creation; `try { ... } finally { await telemetryCapture.FinalizeAsync(job.Id, ct); }`.

### API
- `GET /api/jobs/{id}/telemetry` in `JobEndpoints`: returns `JobTelemetryBundle` (live parse if temp file exists; otherwise DB rows ordered), with same ownership/admin authorization as job GET. Include `hasTelemetry` flag optionally in `JobStatusDto`? Add light count maybe.

### UI
- Admin `JobDetail.cshtml`: add Traces/Metrics section that lazily fetches `/api/jobs/{id}/telemetry` and renders a sortable table (time, type/signal, name, duration, parent, attributes JSON).
- Account `Index.cshtml`: in job details modal, fetch telemetry and render the same compact view; poll while job is active (extend existing 5s job poll to also refresh telemetry when modal open, or add separate poll).

### Blast radius
- `src/Symphony.Data/*` (entities, DbContext), migrations all providers, DTOs, views, controllers/endpoints, JobProcessor, CopilotAgent, configuration service.
- Tests: existing `JobPersistenceTests`, configuration tests (if any), Copilot probe unaffected. Add focused tests for telemetry normalization and retention maybe.

### Planned commits
1. `docs: plan job telemetry feature in TODO.md`
2. `feat(data): add JobTelemetry entity, UserConfiguration toggle, migrations`
3. `feat(config): plumb telemetry content toggle through DTO/service/view`
4. `feat(telemetry): add in-process capture service and CopilotAgent wiring`
5. `feat(processing): begin/finalize telemetry per job`
6. `feat(api): add /api/jobs/{id}/telemetry endpoint`
7. `feat(ui): render traces/metrics in admin and account job detail`
8. `test: add telemetry normalization/retention coverage`
9. `docs: remove TODO.md — plan executed`

### GitHub issues log
- [x] #103 — Codex agent telemetry once Codex support is enabled (follow-up).
- [x] #104 — live OTLP receiver + SignalR streaming for true real-time telemetry beyond polling (follow-up).

## Grounding notes
- Copilot SDK uses `CopilotClientOptions.Telemetry` with `FilePath`/`ExporterType="file"` (validated by probe: 116 records produced).
- File exporter emits JSONL `span` and `metric` record types; parsing is straightforward via `System.Text.Json`.
- Retention is app-level; no provider-specific DB function assumptions beyond standard EF LINQ.

## Verification
- Build all projects after each commit.
- Run probe `dotnet run --project tests/Symphony.CodingAgentsProbe -- otel`.
- Run filtered tests when approved.
- Playwright MCP smoke: open job detail, verify telemetry section loads (admin + user).
