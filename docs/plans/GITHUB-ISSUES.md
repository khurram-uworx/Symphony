# GitHub Issues Support Plan

This document captures the plan for adding GitHub Issues as a supported issue tracker in Symphony.

## Goal

Add `tracker.kind: github` while preserving the existing `IIssueTracker` abstraction and the orchestrator's state-based flow.

## Configuration design

- `tracker.kind: github`
- `tracker.project_slug`: GitHub repository in `owner/repo` form, for example `khurram-uworx/Symphony`
- `tracker.endpoint`: GitHub API base URL
  - Default: `https://api.github.com`
  - Supports GitHub Enterprise: `https://github.example.com/api/v3`
- `tracker.api_key`: GitHub token
  - Supports direct values and `$ENV_VAR` expansion
  - Supports fallback to `GITHUB_TOKEN` or `GH_TOKEN`

Example:

```yaml
tracker:
  kind: github
  project_slug: "owner/repo"
  endpoint: "https://api.github.com"
  api_key: $GITHUB_TOKEN
  active_states:
    - open
  terminal_states:
    - closed
```

## State model

GitHub Issues has only native `open` and `closed` states, while Symphony uses configurable state names.

Recommended first iteration:
- candidate issues are fetched with `state=open`
- terminal issues are fetched with `state=closed`
- parsed issue state is `open` or `closed`
- optional label-based state mapping can refine workflows by preferring a GitHub label whose name matches a configured state

This allows simple `open` / `closed` configurations and richer label-based workflows later.

## Architecture

### Tracker client

Add a new tracker implementation:

```text
src/Symphony.App/IssueTracker/GitHubIssuesClient.cs
```

It should implement `IIssueTracker`:

- `FetchCandidateIssuesAsync`
- `FetchTerminalIssuesAsync`
- `FetchIssueStatesByIdsAsync`

HTTP requirements:
- Use `IHttpClientFactory`
- Set `Authorization: Bearer <token>`
- Set `Accept: application/vnd.github+json`
- Set `X-GitHub-Api-Version: 2022-11-28`
- Set `User-Agent`

Parsing requirements:
- Skip pull requests by checking for the `pull_request` property
- Paginate using `page` and `per_page=100`
- Stop pagination when fewer than 100 issues are returned
- Map `number` to `Issue.Id` and `Issue.Identifier`
- Map `title`, `body`, `state`, `labels`, `html_url`, `created_at`, and `updated_at`
- Leave priority, branch name, and blockers empty in the first iteration

### Configuration validation

Update `ServiceConfigProvider.ValidateForDispatch`:
- Allow `github` as a tracker kind
- For GitHub, validate that `project_slug` has the form `owner/repo`
- Require a non-empty `api_key`

### Dependency injection

Update `Program.cs` to instantiate `GitHubIssuesClient` when `tracker.kind == github`, analogous to Linear and Jira.

## State transitions

Symphony does not update issue state through `IIssueTracker`; state changes are performed by the coding agent. GitHub support should document that agents use:
- GitHub MCP, or
- the `gh` CLI

For updates. The orchestrator relies on:
- active states such as `open` or labels representing states
- terminal state `closed`

## Tests

Planned test coverage:
- config fallback and `owner/repo` validation
- issue parsing
- PR filtering
- pagination termination
- optional live tests gated by `GITHUB_TOKEN` and `GITHUB_TEST_REPO`

## Documentation

Update:
- `README.md`
- `docs/CONFIG.md`

Document:
- `tracker.kind: github`
- `project_slug: owner/repo`
- supported state values
- token scopes:
  - tracker: issue read access
  - agent: issue write access for status/label updates
- use of `gh` or GitHub MCP

## Risks and open decisions

- GitHub issue numbers are repo-scoped, which is sufficient for one repository per workflow file
- Blocker relationships are not parsed in the first iteration
- Rate limits and retry/backoff can be improved later
- Label-based state semantics should be carefully documented to avoid surprising behavior
