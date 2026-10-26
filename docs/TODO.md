# Plan: git tooling and automatic repository checkout

Branch: `khurram/git-tooling`

## Problem

1. The container image (`src/Symphony/Dockerfile`) ships no `git`, so the only clone path
   (the `after_create` hook in `src/Symphony/WORKFLOW.md`) fails inside the published image.
2. Cloning is workflow-owned, not job-owned. Nothing in code guarantees that a workspace is a
   git checkout; a user can delete the hook, and the failure surfaces as a generic
   `after_create hook failed` without the git stderr.
3. The coding agent cannot authenticate git. `CopilotAgent` builds `clientOptions.Environment`
   only when `tracker.kind == github`, and never sets git's `http.extraheader`. Even in the
   GitHub-tracker case the agent gets `GH_TOKEN` at best, so `gh` works but `git push` fails.
4. The job snapshot PAT never reaches the agent. Only `config.Tracker.ApiKey` is used, so
   work-item jobs with `tracker.kind: none` or Linear get no GitHub credential at all.
5. No git identity exists in a clean container, so `git commit` fails with
   "Author identity unknown".
6. The container has no `node`/`npx`, so the Linear MCP stdio server
   (`npx -y mcp-remote`) cannot start. Headless Linear auth is a separate issue.
7. A failed or partial clone is never retried: `createdNow` is false on the next attempt, so
   the agent can run inside a broken directory.
8. Clone duration is bounded by the 60 s hook timeout.
9. `GitHubEnvironment.Build` forces `GIT_CONFIG_COUNT=1`, discarding any `GIT_CONFIG_*`
   already present in the base environment.
10. Existing saved workflows still contain the clone hook, so moving the clone into code
    would double-clone and fail.

## Decisions (from the human)

- Shipped `WORKFLOW.md` drops the clone hook. Hooks run first; the platform then ensures a
  checkout exists, so a workflow that still has the old clone line keeps working unchanged
  and the ensure step is a no-op. If that legacy hook fails, the platform retries the clone
  itself before failing the job.
- Image toolchain: `git`, `gh`, `node`/`npm`, and basic shell utilities only. No .NET SDK.
- Commit identity: resolve the GitHub login with the job PAT, fall back to a static identity.
- Linear MCP headless auth is out of scope (separate issue).

## Proposed changes

### 1. Configuration model

`src/Symphony/Config/ServiceConfig.cs`

```csharp
public record GitIdentity(string Name, string Email);

public record RepositoryConfig(
    GitHubRepository Repository,
    string? Token,
    GitIdentity? Identity = null,
    string? CloneUrl = null);

public record ServiceConfig(
    ...,
    CopilotConfig? Copilot,
    RepositoryConfig? Repository = null);
```

`WorkspaceConfig` gains `CloneTimeoutMs` (`workspace.clone_timeout_ms`, default `300000`).

### 2. Identity resolution

New `src/Symphony/Repositories/GitHubIdentityResolver.cs`:

- `GET https://api.github.com/user` with the PAT, 5 s timeout.
- In-memory cache keyed by a token hash.
- Fallback identity: Symphony user name and `<user>@users.noreply.github.com`.
- Best effort: a failure never blocks a job.

### 3. Job wiring

`src/Symphony/Processing/JobProcessor.cs`

- Build `RepositoryConfig` from the decrypted snapshot, falling back to
  `userConfig.GitHubPersonalAccessToken`.
- Attach the resolved identity.
- Pass it through `config with { Repository = ... }`.
- Remove the `workspaceManager.GitHubEnvironment = ...` assignment.

### 4. Workspace ensure-checkout

`src/Symphony/Workspaces/WorkspaceManager.cs`

- Drop the mutable `GitHubEnvironment` property; read `config.Repository`.
- Order: create directory → run optional `after_create` hook → ensure a checkout exists.
- Ensure step: if `.git` is missing, run
  `git clone --depth 1 --single-branch --no-tags <url> .` with the GitHub environment,
  bounded by `workspace.clone_timeout_ms`.
- A failed hook is a warning when the ensure step then succeeds.
- A failed clone deletes the partial directory and throws `WorkspaceCloneException` with
  redacted stderr.

### 5. Shared environment

`src/Symphony/Repositories/GitHubEnvironment.cs`

- Always set `GIT_TERMINAL_PROMPT=0`.
- Add `GH_PROMPT_DISABLED=1` and `GH_NO_UPDATE_NOTIFIER=1`.
- Add `GIT_AUTHOR_NAME/EMAIL` and `GIT_COMMITTER_NAME/EMAIL` from the identity.
- Append `GIT_CONFIG_*` entries instead of forcing the count to 1.

### 6. Agent environment

`src/Symphony/Agent/CopilotAgent.cs`

- Build `clientOptions.Environment` from `config.Repository` for every job that has a
  repository, removing the `tracker.kind == github` gate.
- Keeps `GITHUB_REPOSITORY` for `gh`.

### 7. Workflow default

`src/Symphony/WORKFLOW.md`: remove the `hooks:` block.

### 8. Container

`src/Symphony/Dockerfile` runtime stage:

- Install `git`, `gh`, `ca-certificates`, `curl`, `openssh-client`, `jq`, `unzip`,
  `nodejs`, `npm` next to the krb5 libraries.
- Create `/home/app/workspaces` owned by `app`.
- `ENV SYMPHONY_WORKSPACE_ROOT=/home/app/workspaces`.

`samples/docker-compose.yml` and `docker-compose.yml`: pass the workspace root and mount a
named `workspaces` volume.

### 9. Docs

`docs/CONFIG.md`, `docs/FEATURES-GITHUB.md`, `docs/ARCHITECTURE.md`,
`CONTAINER-README.md`.

## Blast radius

| Area | Impact |
| --- | --- |
| `ServiceConfig` | New optional record member; all positional constructions keep compiling. Tests construct it directly. |
| `WorkspaceManager` | Removes a public property. Only `JobProcessor` sets it. Workspace tests construct the manager directly. |
| `GitHubEnvironment` | Shared by workspace hooks and the agent; covered by `GitHubEnvironmentTests`. |
| `CopilotAgent` | Environment now built for any job with a repository. No change to provider/session logic. |
| `JobProcessor` | Adds identity resolution (network, best effort) before workspace creation. |
| Container | New packages and a workspace directory/volume; no change to ports or entrypoint. |
| Docs/workflow | Default workflow loses the clone hook; existing saved workflows keep working. |

## Verification

1. `dotnet build Symphony.slnx`.
2. Targeted tests: `WorkspaceManager` clone/ensure/hook ordering, `GitHubEnvironment`,
   `GitHubIdentityResolver` fallback, `CopilotAgent` environment builder.
3. Default filtered test run (`Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness`).
4. Rebuild the image from the branch, run the compose stack, confirm `git`/`gh`/`node`
   inside the container.
5. Submit a real job with a repository and PAT; assert clone, identity, commit, push.
6. Grep container logs for the PAT and confirm `.git/config` has no credential.

## Planned commits

1. `docs: plan git tooling and automatic repository checkout in TODO.md`
2. `feat(config): add repository and git identity to service config`
3. `feat(repositories): resolve GitHub identity from the job token`
4. `feat(workspaces): ensure a shallow checkout when creating a workspace`
5. `feat(agent): pass the repository environment to the coding agent`
6. `feat(workflow): drop the clone hook from the default workflow`
7. `build(container): install git, gh, and node in the runtime image`
8. `docs: document automatic checkout and container tooling`

## GitHub issues log

- [x] #109 — Linear MCP uses interactive OAuth and a shared `~/.mcp-auth`, so it cannot run
      headlessly in a container and leaks credentials across jobs.
      https://github.com/khurram-uworx/Symphony/issues/109

## Progress log

- Commits: plan, repositories, config, workspaces, agent, workflow, container, tests, docs.
- `dotnet build Symphony.slnx`: 0 errors.
- Filtered test run: 111 passed, including a real local bare-repo clone test.


## Reminder

When deferred work or a concern appears during execution, create a GitHub issue immediately
(`gh issue create --repo khurram-uworx/Symphony`) and record the number here. Do not hold it
in memory and do not wait until the end of the plan.
