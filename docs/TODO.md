# Plan: Live OTLP receiver for job telemetry (SignalR streaming) (#104)

## Problem

Job telemetry today is captured through a per-job JSONL file. `JobTelemetryCapture` creates a temp file, `CopilotAgent` points the SDK's file exporter at it, reads on demand re-parse it on every poll, and `FinalizeAsync` persists it. This gives only 5s-polling "live-ness" and depends on disk I/O in the host.

Follow-up goal: replace the file spool with an in-process OTLP/HTTP receiver plus SignalR push so active job telemetry streams in real time, while preserving persistence + retention.

## Grounding notes

- Copilot SDK `TelemetryConfig` supports `OtlpEndpoint`, `OtlpProtocol`, `FilePath`, `ExporterType`, `SourceName`, `CaptureContent` (verified by reflection).
- SDK posts OTLP/HTTP **JSON** to `{baseUrl}/v1/traces` and `/v1/metrics` with `User-Agent: OTel-OTLP-Exporter-Rust/...`; content-type `application/json`.
- `OTEL_RESOURCE_ATTRIBUTES` is honored and lands in `resource.attributes`, including `job.id` when set in the client environment (verified by a local receiver probe).
- SignalR hub infrastructure already exists (`JobUpdateHub` at `/hubs/jobs`, `AddSignalR()` in `Program.cs`).
- Data model + retention unchanged; no migration required.

## Decisions (from human)

1. Content capture remains user-controlled via `TelemetryContentEnabled` (unchanged).
2. Receiver accepts JSON only; protobuf is out of scope (default SDK transport is JSON).
3. Reuse the existing `JobUpdateHub` / `/hubs/jobs` connection for telemetry subscriptions to avoid a second connection.
4. Unknown/non-active job ids are acknowledged with 200 and ignored (no 4xx) to avoid SDK retry storms.
5. OTLP receiver base URL defaults to `http://127.0.0.1:5080`, configurable via `Otlp:ReceiverBaseUrl`.

## Proposed changes

### Capture service (`src/Symphony/Services/JobTelemetryCapture.cs`)
- Replace file path/dictionary logic with a `ConcurrentDictionary<Guid, ConcurrentQueue<JobTelemetryEntry>>` in-memory buffer per job.
- Constructor: `JobTelemetryCapture(IDbContextFactory<SymphonyDbContext>, string receiverBaseUrl)`.
- `BeginJob`, `CurrentJobId`, `CurrentContentEnabled`, `ReceiverBaseUrl`.
- `AppendEntry(Guid jobId, JobTelemetryEntry entry)` used by the receiver.
- `ReadAsync`: active buffer snapshot if present; else DB rows (unchanged contract).
- `FinalizeAsync`: drain buffer → persist → apply retention → clear state.
- Static parse helpers + job-id extraction for unit tests: `TryGetJobId(JsonDocument doc)`, `ParseOtlpTraces(string)`, `ParseOtlpMetrics`, `ParseOtlpLogs`.

### Agent wiring (`src/Symphony/Agent/CopilotAgent.cs`)
- When a capture is present, build env as today, add `OTEL_RESOURCE_ATTRIBUTES=job.id={CurrentJobId},service.name=symphony`, and configure
  `TelemetryConfig { OtlpEndpoint = capture.ReceiverBaseUrl, OtlpProtocol = "json", SourceName = "symphony", CaptureContent = capture.CurrentContentEnabled }`.

### Receiver endpoints (`src/Symphony/Api/OtlpEndpoints.cs`)
- `POST /v1/traces`, `/v1/metrics`, `/v1/logs` (tolerant of prefixed paths).
- Parse JSON, extract `job.id`; if job not active → 200 ignored.
- Map spans/metrics/logs → `JobTelemetryEntry`, buffer, fire SignalR push to `telemetry:{jobId}`.

### SignalR (`src/Symphony/Hubs/JobUpdateHub.cs`)
- Add `SubscribeTelemetry(Guid jobId)` with ownership/admin check, then join `telemetry:{jobId}`.

### DI / routing (`Program.cs`)
- Register capture with receiver base URL; map OTLP endpoints.

### UI
- `JobDetail.cshtml` + `Account/Index.cshtml`: connect SignalR to `/hubs/jobs`, subscribe, and append streaming rows (keep fetch for initial load).

### Tests
- `tests/Symphony.Tests`: OTLP parse + job-id correlation unit tests (no network/DB).

## Blast radius

- `Services/JobTelemetryCapture.cs` (core), `Agent/CopilotAgent.cs`, `Agent/CodingAgentFactory.cs` signature unaffected.
- `Program.cs` DI + routing.
- `Hubs/JobUpdateHub.cs` gains one method.
- Views JS changes; no DB/migrations.
- Existing `JobTelemetryCapture` callers (`JobProcessor`, `JobEndpoints`) keep contracts (`BeginJob`, `ReadAsync`, `FinalizeAsync`, `CurrentContentEnabled`).

## Planned commits

1. `feat(telemetry): replace file spool with in-memory OTLP buffer`
2. `feat(agent): switch CopilotAgent to OTLP export`
3. `feat(api): add in-process OTLP/HTTP receiver`
4. `feat(signalr): push incoming telemetry entries to job subscribers`
5. `feat(ui): stream telemetry over SignalR`
6. `test(telemetry): cover OTLP parsing and job-id correlation`
7. `docs: update features note for live telemetry`

## GitHub issues log

- (none captured yet during planning)

## Verification

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (ask first)
- Manual smoke via Playwright MCP against a running host after implementation.
