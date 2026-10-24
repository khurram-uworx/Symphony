# Configuration

Symphony is configured through a Markdown workflow file (by default `WORKFLOW.md`) that combines YAML front matter with a prompt template. The application does not use a separate `appsettings.json`; all runtime settings come from this file plus a small set of environment variables.

## Workflow file format

The file has two parts:

1. YAML front matter delimited by `---`.
2. A Markdown prompt body that can use template variables such as `{{ issue.identifier }}`.

Example:

```markdown
---
tracker:
  kind: linear
  project_slug: "YOUR_PROJECT_SLUG"
workspace:
  root: ~/symphony/workspaces
agent:
  max_turns: 20
---

You are working on issue {{ issue.identifier }}: {{ issue.title }}
Description: {{ issue.description }}
```

## Where configuration is loaded

- `WorkflowPathResolver` resolves the workflow file path from CLI args (defaulting to `./WORKFLOW.md`).
- `WorkflowManager` loads and watches the file.
- `ServiceConfigProvider.GetConfig()` converts the YAML into typed configuration records.
- `ServiceConfigProvider.ValidateForDispatch()` validates required values before an issue is dispatched.

## Configuration sections

### tracker

| Key | Description | Default |
| --- | --- | --- |
| `kind` | Issue tracker type: `linear` or `jira` | (required) |
| `endpoint` | Tracker API endpoint | `https://api.linear.app/graphql` |
| `api_key` | Tracker credential (see Credentials below) | (required / validated) |
| `project_slug` | Tracker project identifier | (required / validated) |
| `active_states` | Issue states eligible for processing | `Todo`, `In Progress` |
| `terminal_states` | Issue states that stop processing | `Closed`, `Cancelled`, `Canceled`, `Duplicate`, `Done` |

### polling

| Key | Description | Default |
| --- | --- | --- |
| `interval_ms` | Polling interval in milliseconds | `30000` |

### workspace

| Key | Description | Default |
| --- | --- | --- |
| `root` | Root directory for issue workspaces; supports `~` | OS temp path + `symphony_workspaces` |

### hooks

| Key | Description | Default |
| --- | --- | --- |
| `after_create` | Shell command run after workspace creation | none |
| `before_run` | Shell command run before agent execution | none |
| `after_run` | Shell command run after agent completion | none |
| `before_remove` | Shell command run before workspace removal | none |
| `timeout_ms` | Timeout for hook execution | `60000` |

### agent

| Key | Description | Default |
| --- | --- | --- |
| `max_concurrent_agents` | Maximum concurrent agent runs | `10` |
| `max_retry_backoff_ms` | Maximum retry backoff | `300000` |
| `max_turns` | Maximum consecutive agent turns | `1` |
| `max_concurrent_agents_by_state` | Per-state concurrency limits | none |

### codex (optional)

If present, Symphony uses `CodexAgent`; otherwise it uses `CopilotAgent`.

| Key | Description | Default |
| --- | --- | --- |
| `command` | Command to launch Codex app-server | `codex app-server` |
| `approval_policy` | Approval policy passed to Codex | none |
| `thread_sandbox` | Thread sandbox setting | none |
| `turn_sandbox_policy` | Turn sandbox policy | none |
| `turn_timeout_ms` | Turn timeout | `3600000` |
| `read_timeout_ms` | Read timeout | `5000` |
| `stall_timeout_ms` | Stall timeout | `300000` |

## Credentials and secrets

Symphony reads tracker credentials from configuration and/or the environment. Values are resolved by `ConfigReader.ResolveEnvValue`: if a value starts with `$`, the rest is treated as an environment variable name.

### `tracker.api_key`

Source resolution order:

1. If `tracker.api_key` is set, use it (literal value).
2. If it is missing for **Linear**, Symphony also checks the `LINEAR_API_KEY` environment variable directly.
3. If the value starts with `$` (for example `$LINEAR_API_KEY`), resolve it from the named environment variable.

#### Linear

- `LinearClient` sends `tracker.api_key` unchanged as the HTTP `Authorization` header.
- Recommended configuration:

```yaml
tracker:
  kind: linear
  api_key: $LINEAR_API_KEY
```

or (for backwards compatibility) rely on the implicit `LINEAR_API_KEY` environment variable when `api_key` is omitted.

### Jira

- `JiraClient` expects `tracker.api_key` in one of these forms:
  - `username:token` (split into basic authentication username/password)
  - `token` only (basic auth is then performed with `project_slug` as the username and the token as the password)
- The Jira endpoint is provided through `tracker.endpoint`.

### Codex agent

- Symphony does not store Codex credentials in the workflow file.
- `CodexAgent` launches `codex app-server` as a subprocess, so authentication is handled by the Codex CLI's own environment and login state.

### Copilot agent

- Symphony does not store Copilot credentials in the workflow file.
- `CopilotAgent` creates a `CopilotClient` and relies on the GitHub Copilot runtime/CLI authentication and environment state.

### Validation

`ServiceConfigProvider.ValidateForDispatch` requires:

- `tracker.kind` is `linear` or `jira`
- `tracker.api_key` is non-empty
- `tracker.project_slug` is non-empty

## Environment variables

- `LINEAR_API_KEY` — Linear (and fallback Jira-style) API token. Used when `tracker.api_key` is missing or referenced as `$LINEAR_API_KEY`.
- `SYMPHONY_WORKSPACE_ROOT` — optional; to use it, set `workspace.root: $SYMPHONY_WORKSPACE_ROOT` in `WORKFLOW.md`. The application itself resolves workspace paths through `workspace.root`.
- Standard environment variables used by the Codex/Copilot CLIs for their own authentication.

## Allowed workflow front matter overrides

When a `ServiceConfig` is built from saved user configuration plus workflow content,
only the following front matter keys may override the user-level configuration:

- `workspace.root`
- `workspace_root`
- `hooks.after_create`
- `hooks.before_run`
- `hooks.after_run`
- `hooks.before_remove`
- `hooks.timeout_ms`
- `agent.max_turns`
- `agent.max_concurrent_agents_by_state`
- top-level `max_turns`
- top-level `max_concurrent_agents_by_state`

The tracker (`kind`, `endpoint`, `api_key`, `project_slug`, active/terminal states),
polling, Codex, and Copilot settings are not replaced by workflow front matter.

Malformed YAML front matter fails parsing and no overrides are applied.
EOF

