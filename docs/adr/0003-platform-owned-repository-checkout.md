# ADR-0003: Platform-owned repository checkout

Status: Accepted

## Context

Until now the repository checkout was a workflow hook. The default `WORKFLOW.md` carried:

```yaml
hooks:
  after_create: |
    git clone --depth 1 https://github.com/${GITHUB_REPOSITORY} .
```

That put a required job step in user-editable content, so deleting the hook produced a job that ran inside an empty directory, and it surfaced failures as a generic `after_create hook failed` without the git stderr. It was also bounded by the hook timeout (`hooks.timeout_ms`, 60 s), which is short for a clone. Three further gaps made the checkout unusable:

- The container image shipped no `git`, so the hook could not run in the published image.
- `CopilotAgent` only built its environment for `tracker.kind: github`, and only from the tracker token, so work-item and Linear jobs got no credential and could not `git push`.
- A clean container has no commit identity, so `git commit` failed.

## Decision

- `WorkspaceManager.CreateForIssueAsync` owns the checkout. Order: create the directory, run `hooks.after_create` if configured, then clone when `.git` is missing. A legacy clone hook therefore keeps working and the platform step becomes a no-op. No migration or skip of existing saved hooks.
- The clone is `git clone --depth 1 --single-branch --no-tags <url> .`, bounded by `workspace.clone_timeout_ms` (default 300000, overridable from workflow front matter).
- A failed `after_create` hook does not fail the job when the platform can still produce a checkout. A failed clone removes the partial directory and fails the job with a typed error carrying the git stderr, redacted against the job token.
- The job carries a `RepositoryConfig` (repository, token, resolved identity) on `ServiceConfig`. `GitHubEnvironment` is the only place that converts it to git/gh environment variables, and it serves hooks, workspace creation, and the agent session.
- The token reaches git as `http.https://github.com/.extraheader`, never in the clone URL and never in `.git/config`. `GIT_CONFIG_*` entries already present in the environment are preserved.
- Commit identity is resolved from `GET /user` with the job token and falls back to a static `<user>@users.noreply.github.com` identity; identity resolution never blocks a job.
- The runtime image installs `git`, `gh`, `node`/`npm`, `openssh-client`, `curl`, `jq`, and `unzip`, and sets `SYMPHONY_WORKSPACE_ROOT=/home/app/workspaces`.

## Consequences

- Checkouts survive only as long as the workspace directory; the `workspaces` volume keeps them across container restarts, and losing it only forces a re-clone.
- History is truncated. `git fetch --deepen=<n>` reaches older commits; a full history would cost a much larger clone per job.
- With no token, a job that has a repository fails at clone time with a clear git error instead of failing later inside the agent.
- Headless Linear MCP authentication is not solved by this decision; it is tracked separately (issue #109) and resolved by [ADR-0004](0004-headless-linear-mcp.md).
