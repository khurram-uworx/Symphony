# Plan: headless Linear MCP (issue #109)

## Problem

Issue #109: the Linear MCP integration cannot run headlessly in containers.

1. `CopilotAgent.InitializeAsync` registers Linear as a stdio proxy,
   `npx -y mcp-remote https://mcp.linear.app/mcp`, with no credential. `mcp-remote`
   then starts OAuth 2.1 with a loopback callback. A container has no browser, and the
   resulting tokens are stored in `~/.mcp-auth`, which every Symphony user shares because
   the image runs as a single OS user (`app`).
2. The job never has a Linear credential to send anyway. `ServiceConfigFactory.BuildTracker`
   resolves only `$VAR`, while `TrackerConfig.From` falls back to `LINEAR_API_KEY`. The job
   path (`JobProcessor.ProcessJobAsync`) always takes the `UserConfiguration` branch, so a
   container that sets `LINEAR_API_KEY` and leaves the saved key blank gets an empty token —
   even though `docs/CONFIG.md` documents the fallback.
3. Nothing validates the Linear credential, and `AgentRunner` collapses init failures into
   `"agent session startup error"`, so the operator sees a generic failure.

## Decisions (confirmed with the human)

- Use native Streamable HTTP MCP (`McpHttpServerConfig`) with
  `Authorization: Bearer <tracker api key>`. No `npx`, no `~/.mcp-auth`.
- Fail the job with `linear_credentials_missing` **only when Linear tickets are submitted**.
  Work-item-only jobs run without a Linear MCP server.
- Keep `node`/`npm` in the image.
- Isolate Copilot session state per Symphony user instead of sharing `~/.copilot`.

## Blast radius

- `Symphony.Agent.CopilotAgent.InitializeAsync` — MCP registration, session config, client
  `BaseDirectory`. Single production caller: `CodingAgentFactory.Create` →
  `JobProcessor.TryCreateCodingAgent`.
- `Symphony.Config.ServiceConfig` — new trailing optional `CopilotHome`. All positional
  constructions keep compiling.
- `Symphony.Config.ServiceConfigFactory.BuildTracker` — one production caller
  (`JobProcessor.ProcessJobAsync`). Affects only the job path.
- `Symphony.Processing.JobProcessor.ProcessJobAsync` — new guard and `CopilotHome` wiring.
- `Symphony.Workspaces.WorkspacePath` — new path helper used by `JobProcessor`.
- No change to `LinearClient`, `IssueTrackerFactory`, `AgentRunner`, or the API surface.
- Tests: new `Agent/CopilotMcpServersTests`, new `Config/ServiceConfigFactoryTests`,
  extension of `Workspaces/WorkspacePathSafetyTests`.

## Proposed changes

### 1. `src/Symphony/Agent/CopilotMcpServers.cs` (new)

`internal static` helper, the single place that decides which MCP servers a job gets:

- `LinearServerName = "linear"`, `LinearMcpUrl = "https://mcp.linear.app/mcp"`.
- `Build(ServiceConfig)` returns a map of MCP server configs:
  - Job submits Linear tickets, tracker is Linear, and the key is non-blank -> one
    `McpHttpServerConfig` with `Url`, `Tools = ["*"]`, and
    `Headers["Authorization"] = "Bearer " + key`.
  - Any other job (work-item-only job, Linear tracker without a key, non-Linear tracker) ->
    no server. A work-item job on a Linear account must not open an MCP connection it does
    not need; a credential-less Linear server is also what triggers interactive OAuth.
- `BuildAuthorizationHeader(key)` normalizes an already-prefixed `Bearer ` value.
- `Validate(ServiceConfig, bool requiresLinearTracker)` returns
  `"linear_credentials_missing: set the Linear API key in configuration or provide LINEAR_API_KEY."`
  or `null`.

### 2. `src/Symphony/Agent/CopilotAgent.cs`

- Replace the `npx` block with `McpServers = CopilotMcpServers.Build(config)`.
- Session hardening: `McpOAuthTokenStorage = McpOAuthTokenStorageMode.InMemory`,
  `InteractiveUser = false`, and `OnMcpAuthRequest` cancels with a warning log so a rejected
  key fails immediately instead of waiting on a loopback callback that can never happen.
- Per-user Copilot home: create `config.CopilotHome`, set
  `clientOptions.BaseDirectory`, and drop an inherited `COPILOT_HOME` from the runtime
  environment so the per-user path is authoritative.
- Log the registered server names only; never the header value.

### 3. `src/Symphony/Config/ServiceConfig.cs`

Add trailing optional `string? CopilotHome = null` to the `ServiceConfig` record.

### 4. `src/Symphony/Workspaces/WorkspacePath.cs`

`internal static string CopilotHomeFor(string userRoot)` -> `<userRoot>/.copilot`, guarded by
`EnsureContainedBy`.

### 5. `src/Symphony/Config/ServiceConfigFactory.cs`

`BuildTracker` gains the `LINEAR_API_KEY` fallback, mirroring `TrackerConfig.From`.

### 6. `src/Symphony/Processing/JobProcessor.cs`

- After `hasTickets` and the GitHub overrides:
  `if (CopilotMcpServers.Validate(config, hasTickets) is { } error) throw new InvalidOperationException(error);`
- `scopedConfig` gains `CopilotHome = WorkspacePath.CopilotHomeFor(userRoot)` and
  `SubmitsTickets = hasTickets`, so MCP registration follows the job's work source.

### 7. `src/Symphony/WORKFLOW.md`

The bundled prompt currently makes Linear MCP an unconditional prerequisite. Work-item-only
jobs without a credential must not stop on it.

### 8. Tests

- `tests/Symphony.Tests/Agent/CopilotMcpServersTests.cs` — http transport, URL, bearer header,
  no double prefix, non-linear/blank-key map, `Validate` matrix.
- `tests/Symphony.Tests/Config/ServiceConfigFactoryTests.cs` — `LINEAR_API_KEY` fallback,
  saved key precedence, existing `$LINEAR_API_KEY` resolution.
- `tests/Symphony.Tests/Workspaces/WorkspacePathSafetyTests.cs` — `CopilotHomeFor` containment.

### 9. Docs

- `docs/CONFIG.md` — Linear MCP auth, `linear_credentials_missing`, per-user Copilot home.
- `CONTAINER-README.md` — how to supply the Linear credential, where agent state lives.
- `docs/adr/0004-headless-linear-mcp.md` (new) and the ADR-0003 deferral note.
- `docs/FEATURES.md` — one-line entry.

### 10. Follow-up: gate MCP on the work source (human review, 2026-10-26)

Linear MCP must be attached only when the job's work source is Linear tickets **and** a key is
available. An account-level Linear tracker is not enough: a work-item-only job on that account
does not need Linear, should not open an MCP connection, and should keep the smallest tool
surface. Implementation:

- `ServiceConfig` gains a trailing optional `bool SubmitsTickets = false` (job-scoped flag, same
  pattern as `CopilotHome`).
- `CopilotMcpServers.Build` requires `config.SubmitsTickets` in addition to
  Linear tracker + non-blank key.
- `JobProcessor` sets `SubmitsTickets = hasTickets` on `scopedConfig`.
- Tests: `Build_WorkItemJobWithLinearTrackerAndKey_ReturnsNoServers`.
- Docs: make the work-source condition explicit in `docs/CONFIG.md`, ADR-0004, and
  `CONTAINER-README.md`.

### 11. Follow-up: forward `LINEAR_API_KEY` in the sample stack (Docker smoke, 2026-10-26)

`samples/docker-compose.yml` passed no Linear credential into the app container, so the
documented `LINEAR_API_KEY` path could not work with the sample stack even though
`CONTAINER-README.md` told operators to set it. Add
`LINEAR_API_KEY: ${LINEAR_API_KEY:-}` to the app service, document it in `samples/.env`, and
note the forwarding in `CONTAINER-README.md`.

### 12. Follow-up: give `CopilotAgent` a real logger (Docker smoke, 2026-10-26)

`JobProcessor.TryCreateCodingAgent` called `CodingAgentFactory.Create(config, logger: null, …)`,
so `CopilotAgent` always ran with `NullLogger`: the "Registered MCP server(s)" line and the
headless MCP auth cancellation warning never reached the container log. That is exactly the
diagnostic an operator needs for this feature, and ADR-0004 already promises a logged warning.
Pass the job's `ILoggerFactory` so both lines are emitted.

## Verification

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Docker: rebuild the image, run the sample stack, submit a Linear ticket job with a BYOM
  Copilot provider and a Linear credential.
- Playwright MCP against the host to drive the UI where needed.

## Planned commits

1. `docs: plan headless Linear MCP fix in TODO.md` — `a050e95`
2. `feat(agent): reach Linear over native HTTP MCP with bearer auth` — `52ca073`
3. `fix(jobs): fail fast when a Linear ticket job has no credential` — `1b1d991`
4. `fix(config): fall back to LINEAR_API_KEY for saved user configuration` — `a1384ba`
5. `feat(agent): isolate Copilot session state per user` — `86d953f`
6. `test(agent): cover Linear MCP construction and credential validation` — `82fe49c`
7. `test(config): cover LINEAR_API_KEY resolution in ServiceConfigFactory` — `e4d4e5b`
8. `docs: describe headless Linear MCP and per-user Copilot home` — `c496a8e`
9. `fix(agent): make the per-user Copilot home explicit in the runtime environment` — `9fd2682`
10. `docs: remove TODO.md — plan executed`

## Verification log

- `dotnet build src/Symphony/Symphony.csproj` — 0 errors (2026-10-26).
- `dotnet build tests/Symphony.Tests/Symphony.Tests.csproj` — 0 errors (2026-10-26).
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` — 129 passed, 0 failed (2026-10-26).
- G2 branch review: fixed three findings (explicit `COPILOT_HOME` export, per-user path
  precedence over SDK merge order, test namespace) in `9fd2682`; no regressions found.
- Docker image rebuild and runtime smoke test: pending after push/PR.

## GitHub issues log

- None yet.

As each task executes, capture deferred work or concerns as a GitHub issue via
`gh issue create --repo khurram-uworx/Symphony` and record it here immediately. Do not rely on
memory: compaction can lose items.
