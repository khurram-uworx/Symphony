# GitHub Integration (Issues + Repository)

This document describes Symphony's GitHub support for both repository work and GitHub Issues. See [FEATURES.md](FEATURES.md) for the overall feature list.

## Repository as Work Source

Every job runs inside a Git working copy. On submission, users provide (or configured credentials supply) the GitHub repository and personal access token.

- Repository slugs may be `owner/repo`, HTTPS URLs, or SSH addresses.
- Tokens are stored encrypted per user and are never returned in plaintext.
- `WorkspaceManager` creates per-issue workspaces and injects `GITHUB_REPOSITORY`, `GH_TOKEN`, `GITHUB_TOKEN`, `GIT_TERMINAL_PROMPT`, and git `http.extraheader` via `GitHubEnvironment.Build`.
- `gh` CLI operations inside hooks or the coding agent use these environment variables.

## GitHub Issues as a Work Source

Set `tracker.kind: github` in the user/workflow tracker configuration. Issue ticket IDs are GitHub issue numbers as strings.

- Configuration lives per user via the Account page and `/api/configuration`.
- Fields:
  - `TrackerKind`: `github`
  - `TrackerEndpoint`: defaults to `https://api.github.com`; supports GitHub Enterprise (`https://github.example.com/api/v3`)
  - `TrackerApiKey`: optional separate token
  - `TrackerProjectSlug`: optional repository in `owner/repo` form
- If `TrackerApiKey` is empty, Symphony falls back to the user's GitHub Personal Access Token.
- If `TrackerProjectSlug` is empty, Symphony resolves issues against the repository submitted with the job, enabling multi-repository use.

## State Model

GitHub issues natively use `open` and `closed`. Symphony stores the returned state as-is. Recommended configuration:

```text
active_states: open
terminal_states: closed
```

## Agent Updates

Symphony does not mutate issue state through the tracker client. Coding agents are expected to update GitHub issue status using:

- the `gh` CLI, or
- a configured GitHub MCP server.

The local coding-agent environment contains the repository token where workspace hooks execute; direct Copilot token injection is tracked separately.

## Security Notes

- Tokens are encrypted at rest.
- Repository and tracker configuration are per-user.
- Work submission can reuse the stored GitHub token.
- least-privilege tokens should include issue read access for the tracker function and issue write access for agent updates.

## Tests

Unit coverage includes issue parsing, pull-request filtering, HTTP error handling, and enterprise endpoint URL construction. Live or integration tests should be gated by `GITHUB_TOKEN` and repository access.