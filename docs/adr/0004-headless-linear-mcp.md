# ADR-0004: Headless Linear MCP authentication

Status: Accepted

## Context

Issue #109: Linear MCP could not run headlessly in a container.

`CopilotAgent` registered Linear as a stdio proxy:

```text
npx -y mcp-remote https://mcp.linear.app/mcp
```

with no credential. `mcp-remote` therefore started OAuth 2.1 with a loopback callback. A container has no browser, and any resulting tokens were written to `~/.mcp-auth`, which every Symphony user shares because the image runs as a single OS user (`app`). Two further gaps compounded the failure:

- The job path always builds configuration from the saved `UserConfiguration`. `ServiceConfigFactory.BuildTracker` resolved only literal values and `$VAR` references, while `TrackerConfig.From` already fell back to `LINEAR_API_KEY`. A container that set the environment variable and left the saved key blank produced an empty credential even though the documented behavior said otherwise.
- Nothing validated the credential, and `AgentRunner` collapsed initialization failures into a generic session-startup error.

## Decision

- The Linear MCP server is declared as native Streamable HTTP (`McpHttpServerConfig`) at `https://mcp.linear.app/mcp` with `Authorization: Bearer <tracker api key>`. No `npx`, no proxy, and no `~/.mcp-auth`.
- `CopilotMcpServers` is the single place that decides which MCP servers a job gets. A server is registered only for a Linear tracker with a non-blank key. Every other job gets an empty map.
- A job that submits Linear tickets without a credential fails before the issue fetch and before the agent starts, with `linear_credentials_missing`. Work-item-only jobs never require Linear access.
- `ServiceConfigFactory.BuildTracker` falls back to `LINEAR_API_KEY`, matching `TrackerConfig.From`.
- MCP authentication is headless-only: `McpOAuthTokenStorage` is in-memory, `InteractiveUser` is false, and `OnMcpAuthRequest` cancels with a warning log. A rejected key therefore fails immediately instead of waiting for a callback that cannot happen.
- Copilot session state is isolated per Symphony user. `CopilotClientOptions.BaseDirectory` points at `<user workspace root>/.copilot`, an inherited `COPILOT_HOME` is removed, and the directory is created before the client starts.
- The runtime image keeps `node`/`npm` for unrelated stdio MCP servers; Linear no longer depends on them.

## Consequences

- Linear ticket jobs run unattended with only a tracker API key in configuration or `LINEAR_API_KEY`.
- A missing or rejected credential produces a typed, actionable failure rather than a browser prompt or a generic startup error.
- Agent state, cached MCP configuration, and session files no longer leak between tenant users. Losing the `workspaces` volume also discards that state and only forces a re-clone.
- Upstream `github/copilot-cli#3100` (open) notes that HTTP MCP with an `Authorization` header may attempt OAuth discovery on a 401/400. A valid key avoids the path, and the cancel handler converts a bad key into a fast logged error. If the runtime regresses, the documented fallback is `mcp-remote --header-file` with a per-job `MCP_REMOTE_CONFIG_DIR`.
- No OAuth or token-refresh flow exists for Linear. A short-lived or revoked key must be replaced in configuration.
