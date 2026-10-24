# Milestone 5 — GitHub repository and PAT handling

## Problem

M5 of `docs/plans/SYMPHONY-WEB-PLAN.md` (T35–T38) is the point where Symphony stops
being a Jira/Linear-only host and becomes a GitHub-repository worker. The current code
stores the requested repository and PAT but never uses them:

- `Job.Repository` is written in `JobService.CreateCoreAsync` and never read. Nothing
  carries the requested repository or PAT from the encrypted snapshot to the workspace
  clone or the coding agent.
- The only clone mechanism is the `after_create` hook in the bundled `WORKFLOW.md`, which
  hardcodes `https://github.com/khurram-uworx/symphony` and has no token. `ShellCommandRunner`
  has no environment injection and logs the raw command on failure.
- `CopilotAgent` hardcodes the Linear MCP server and builds `CopilotClient` with default
  options, so a GitHub-repository job would still talk to Linear.
- `JobProcessor` enforces a hardcoded `GlobalConcurrency = 4`. `AgentConfig.MaxConcurrentAgents`
  and `MaxConcurrentByState` are parsed and never used, and the seeded `GlobalConcurrency`
  app setting is dead.
- Repository validation is "non-empty" only; BYOM validation only checks the provider name.
- `JobProcessor.TryCreateCodingAgent` silently falls back to `NoOpCodingAgent`, so a job can
  reach a terminal state having done nothing.
- `WorkspaceManager` containment uses a bare `StartsWith`, so `/data/user1` accepts
  `/data/user10`, and relative workspace roots are silently resolved against the process CWD.

## Scope

T35 (GitHub repository + PAT), T36 (Copilot BYOM), T37 (user-scoped workspaces and
concurrency), T38 (Symphony repository end-to-end) from
`docs/plans/SYMPHONY-WEB-PLAN.md`.

Decisions carried into the plan (previously discussed and approved as recommended):

- **D1** Repository input is canonicalised to `owner/name`. Accepted inputs: `owner/name`,
  `https://github.com/owner/name[.git]`, `git@github.com:owner/name[.git]`. Everything else
  (enterprise hosts, extra path segments, query strings, fragments) is rejected.
- **D2** The PAT reaches the runtime through the hook process environment
  (`GH_TOKEN`, `GITHUB_TOKEN`, `GITHUB_REPOSITORY`, and `GIT_CONFIG_*` extraheader
  authentication). The token never appears in a clone URL, a command line, `<repo>/.git/config`,
  an event payload, or a log line. The bundled workflow keeps cloning through its
  `after_create` hook and uses `$GITHUB_REPOSITORY` / `%GITHUB_REPOSITORY%` — the bundled
  seed is OS-aware so it works under both cmd.exe and bash.
- **D3** T35's retry criterion is satisfied by a `JobService.CreateRetryAsync` primitive that
  copies the encrypted snapshot to a new job with `RetryOfJobId` set, plus graceful failure
  when a snapshot has no PAT. Retry policy/UI/scheduling remains T44 (M7).
- **D4** "Per-user concurrency" means simultaneously *running jobs* per owner. Per-state limits
  apply only to states that are known at claim time (work-item states); ticket-backed jobs are
  governed by the per-user and global limits. `GlobalConcurrency` comes from the seeded
  `AppSetting` with a safe fallback.
- **D5** T38's first end-to-end run uses inline work items (no tracker). "Ticket state refresh"
  moves to M6/M7 because no GitHub Issues tracker exists yet.
- **D6** Workspace roots must be absolute after `~`/environment expansion. Containment checks
  are separator-aware.

## Proposed changes

### 1. Workspace path safety (T37 first slice)

- `Workspaces/WorkspacePath.cs` (new): `RequireAbsoluteRoot`, `EnsureContainedBy(root, path)`,
  separator-aware comparison, rejects roots that are filesystem/drive roots.
- `WorkspaceManager` uses it for create and cleanup; `JobProcessor` uses it for the
  `baseRoot/{ownerUserId}` scope and rejects owner IDs containing separators.
- `ConfigReader.ExpandPath` documents that callers must validate; `WorkspaceConfig.From` throws
  for a relative root before expansion.

### 2. Repository canonicalisation and redaction (T35)

- `GitHub/GitHubRepository.cs` (new): `TryParse` for the accepted forms, `Slug`, `CloneUrl`,
  `Matches`.
- `GitHub/GitHubEnvironment.cs` (new): builds the child-process environment for a
  repository/token pair, including a base64 `AUTHORIZATION: basic` extraheader via
  `GIT_CONFIG_COUNT`/`GIT_CONFIG_KEY_0`/`GIT_CONFIG_VALUE_0`. No token in any URL.
- `WorkRequest.IsValid` validates and canonicalises the repository.
- `JobService.CreateCoreAsync` stores the canonical slug.
- `Security/Redaction.cs`: `RedactSecrets(text, knownSecrets)` for log lines that may echo
  a hook's own output.

### 3. Runtime wiring (T35)

- `JobProcessor.ProcessJobAsync` reads `github.repository` and `github.personalAccessToken`
  from the decrypted snapshot, validates them, and fails the job gracefully with
  `github_credentials_missing` when the snapshot has no PAT.
- `AgentRunner`/`WorkspaceManager` accept an optional GitHub runtime context and pass it as
  environment to `after_create`/`before_run`/`after_run`/`before_remove`.
- `ShellCommandRunner.RunAsync` accepts an environment dictionary and redacts known secrets
  from the failure log line.
- `CopilotAgent` receives the same context and injects it into `CopilotClientOptions.Environment`
  so agent-run `git`/`gh` commands authenticate.

### 4. Retry primitive (T35, D3)

- `JobService.CreateRetryAsync(jobId, userId, isAdmin)` creates a new `Queued` job with
  `RetryOfJobId`, the same repository/workflow, and a copy of the encrypted snapshot.
  Refuses completed-with-no-snapshot jobs and jobs the caller does not own.

### 5. Copilot BYOM (T36)

- `CopilotConfig.IsValid` requires `model_id`, `api_key`, and `base_url` when a provider is
  configured, and validates `wire_api` (`completions`/`responses`, optional).
- `CodingAgentFactory` validates through the same path and stops swallowing configuration errors.
- `CopilotAgent`:
  - builds `CopilotClientOptions` with `WorkingDirectory` and the GitHub environment,
  - adds the Linear MCP server only when `Tracker.Kind == "linear"`,
  - emits an `agent.initialized` event carrying provider/model (never the key) through the
    existing `onEvent` callback,
  - keeps BYOK provider/model wiring as-is (verified against SDK 1.0.26 by `ByomProbe`).
- `JobProcessor` forwards agent events into persisted `agent.event` job events (metadata only).
- `TryCreateCodingAgent` no longer silently degrades: a job with no usable agent fails with
  `agent_unavailable` and an explanatory event instead of "completing" against a no-op agent.
  `NoOpCodingAgent` is retained only for explicitly disabled agents (no Copilot config at all
  and no repository-contacting work) — see the red-flag note below.

### 6. Concurrency (T37)

- `Processing/ConcurrencyLimits.cs` (new): reads `GlobalConcurrency` from `AppSetting`
  (fallback 4), exposes `MaxRunningJobs`, `RunningForOwner`, `RunningForState`.
- `JobProcessor` claims candidates only while total running < global, owner running <
  owner's `MaxConcurrentAgents`, and (for work-item jobs) state running < per-state limit.
- Seeded `AppSetting` value is honoured instead of the constant.

### 7. Docs and bundled workflow (T38)

- `src/Symphony/WORKFLOW.md`: `after_create` clones `$GITHUB_REPOSITORY`/`%GITHUB_REPOSITORY%`,
  no hardcoded repo, tracker section stays optional.
- `docs/CONFIG.md`: document the GitHub runtime contract, env names, BYOM requirements, and
  concurrency settings; remove the stale Codex-selection text.
- `docs/FEATURES.md`, `docs/ARCHITECTURE.md`, `README.md`: align with the implemented behavior.
- `docs/plans/SYMPHONY-WEB-PLAN.md`: tick T35–T37 acceptance where met and leave T38's manual
  run marked as requiring a human-supplied PAT.

## Blast radius

- Runtime job path: `JobProcessor`, `AgentRunner`, `WorkspaceManager`, `ShellCommandRunner`,
  `CopilotAgent`, `CodingAgentFactory`, `JobService`, `WorkRequest` DTOs.
- Config: `ServiceConfig`, `ServiceConfigFactory`, `CopilotConfig`, `WorkspaceConfig`,
  `ConfigReader.ExpandPath` callers.
- Data: no schema changes (snapshot retry reuses `RetryOfJobId`).
- Docs/bundled workflow: `WORKFLOW.md`, `docs/CONFIG.md`, `docs/FEATURES.md`,
  `docs/ARCHITECTURE.md`.
- Tests: new `GitHubRepositoryTests`, `WorkspacePathSafetyTests`, `GitHubEnvironmentTests`,
  `JobServiceRetryTests`, `CopilotConfigTests`, `JobConcurrencyTests`; existing
  `JobServicePersistenceTests`, `JobPersistenceTests`, config/workflow tests must stay green.
- Behaviour change: jobs with no Copilot provider now fail instead of no-op "succeeding".

## Verification

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Manual: launch the host (`dotnet run --project src/Symphony`), log in as the seeded admin,
  submit a job with a repository and PAT, confirm the workspace clones, hooks receive
  `GH_TOKEN`, the agent initialises, the job reaches a terminal state, and no PAT appears in
  events/status/log output.
- Playwright MCP smoke of the run launcher, job detail, and configuration pages.

## Planned commits

1. `docs: plan milestone 5 (GitHub repo + PAT) in TODO.md`
2. `Reject unsafe workspace roots and path escapes`
3. `Canonicalise GitHub repository input and add PAT redaction`
4. `Pass request-scoped GitHub repository and PAT to the job runtime`
5. `Add job retry from the encrypted request snapshot`
6. `Validate Copilot BYOM settings and report agent metadata`
7. `Enforce per-user, per-state, and global job concurrency`
8. `Document the GitHub runtime contract and refresh M5 docs`
9. `docs: remove TODO.md — plan executed` (after the G2 reviews)

## GitHub issues log

- [ ] none yet

Reminder: as each task executes, create a GitHub issue immediately
(`gh issue create --repo khurram-uworx/Symphony`) for any deferred work or concern and
record its number here. Do not rely on memory — compaction can lose it.
