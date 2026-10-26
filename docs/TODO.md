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
  - Linear tracker with a non-blank key -> one `McpHttpServerConfig` with `Url`,
    `Tools = ["*"]`, and `Headers["Authorization"] = "Bearer " + key`.
  - Linear tracker without a key -> no server (never a credential-less linear server, which
    is what triggers interactive OAuth).
  - Any other tracker -> empty map.
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
- `scopedConfig` gains `CopilotHome = WorkspacePath.CopilotHomeFor(userRoot)`.

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

## Verification

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Docker: rebuild the image, run the sample stack, submit a Linear ticket job with a BYOM
  Copilot provider and a Linear credential.
- Playwright MCP against the host to drive the UI where needed.

## Planned commits

1. `docs: plan headless Linear MCP fix in TODO.md`
2. `feat(agent): reach Linear over native HTTP MCP with bearer auth`
3. `fix(jobs): fail fast when a Linear ticket job has no credential`
4. `fix(config): fall back to LINEAR_API_KEY for saved user configuration`
5. `feat(agent): isolate Copilot session state per user`
6. `test(agent): cover Linear MCP construction and credential validation`
7. `test(config): cover LINEAR_API_KEY resolution in ServiceConfigFactory`
8. `docs: describe headless Linear MCP and per-user Copilot home`
9. `docs: remove TODO.md — plan executed`

## GitHub issues log

- None yet.

As each task executes, capture deferred work or concerns as a GitHub issue via
`gh issue create --repo khurram-uworx/Symphony` and record it here immediately. Do not rely on
memory: compaction can lose items.
