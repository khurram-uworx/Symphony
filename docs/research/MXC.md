# Microsoft eXecution Container (MXC)

Research snapshot: 2026-10-26. Tracking issue: [#118](https://github.com/khurram-uworx/Symphony/issues/118).
Purpose: evidence base for deciding whether Symphony should contain agent execution with MXC. The decision itself is deferred to an ADR that is written only after the probe in
[MXC-PROBE.md](../MXC-PROBE.md) passes. This document records what MXC is, what it does and does not cover,
how GitHub Copilot already uses it, what Symphony would have to change, and which questions remain open.

## Verdict in one paragraph

MXC is a credible, shipping containment layer, and it is the sandbox behind GitHub Copilot's local sandboxing,
which went GA on 2026-10-07. That precedent validates the direction for Symphony. The important correction to
the obvious plan ("run every agent inside MXC") is that Microsoft does not sandbox the agent harness. It
sandboxes the processes the agent launches: shell commands, local MCP servers, and local language servers. The
harness itself stays outside and enforces policy on its in-process file tools on a best-effort basis. Symphony
should follow the same shape: enable the Copilot CLI's own sandbox, and use MXC directly only for Symphony-owned
shells (clone and hooks). Codex has its own sandbox configuration and is out of scope. MXC is containment, not a
security boundary against a compromised harness, and it is strongest on a developer workstation and weakest
inside the container the project actually ships.

## What MXC is

An open-source (MIT) Microsoft project that applies a declarative containment policy to a workload and its
child processes. It is a library that builds into the consuming application, not a daemon and not a container
runtime.

```
App -> MXC SDK (Rust/.NET/Node, in process) -> selected backend -> isolated workload
```

A caller supplies a workload command, a set of restrictions (filesystem, network, UI), and optional
backend-specific configuration. MXC validates the request, picks a backend, and launches the workload.

- Repository: <https://github.com/microsoft/mxc>
- .NET package: [`Microsoft.Mxc.Sdk`](https://www.nuget.org/packages/Microsoft.Mxc.Sdk) 1.0.1 (2026-10-16).
  1.0.0 shipped 2026-10-07. Targets `net8.0` and runs on `net10.0`. No package dependencies. Roughly 14 MB,
  because the platform-native runtime assets are bundled. The versioned public surface is
  `Microsoft.Mxc.Sdk.V1`.
- Repo signals at snapshot: ~3.0k stars, 165 forks, 536 commits on `main`, 65 open issues, 23 open pull
  requests, MIT licence, support policy limited to GitHub issues.

## Backends and host requirements

| Host | Default backend | Other backends | Notes |
| --- | --- | --- | --- |
| Windows 11 x64/ARM64 | `processcontainer` | `windows_sandbox`*, `wslc`, `microvm`*, `hyperlight`*, `isolation_session` | Needs a recent cumulative update |
| Linux x64/ARM64 | `bubblewrap` | `lxc`, `microvm`, `hyperlight` | No default network isolation for children |
| macOS ARM64/x64 | `seatbelt` | none | Process-scoped profile per command |

\* experimental.

Windows support is gated by OS build, not by feature flags:

| Windows 11 release | ProcessContainer minimum | IsolationSession minimum |
| --- | --- | --- |
| 24H2 | 26100.9278 | 26100.9550 |
| 25H2 | 26200.9278 | 26200.9550 |
| 26H2 | 26300.9550 | 26300.9550 |
| 26H1 | 28000.2804 | 28000.3086 |

Other host facts that matter operationally:

- `IsolationSession` cannot enforce network restrictions at all; it requires explicitly unrestricted networking.
- PTY mode is available for `IsolationSession`, `bubblewrap`, `lxc`, and `seatbelt` direct execution. PTY support
  for existing containers is `IsolationSession`-only.
- Windows supports `MxcContainer.Probe()` (evaluate a ProcessContainer request, including isolation tier, without
  creating a container) and denial capture (`processContainer.captureDenials`) that depends on runtime capability
  probing rather than a fixed OS list.
- Telemetry is off unless the individual run opts in, the Windows user has consented, administrative policy
  permits collection, and the application enables it in the request. It is a no-op outside Windows.
- `--audit` and `--debug` exist for the standalone executors. `--audit` disables all sandbox security and must
  never be used on untrusted code; `--debug` only adds diagnostics.

## SDK surface (`.NET V1`)

Operations:

| Task | API |
| --- | --- |
| Create and run, capture output | `MxcContainer.Run` / `RunAsync` -> `ExecutionResult` |
| Create and run, live pipes | `MxcContainer.Spawn` / `SpawnAsync` -> `MxcProcess` |
| Create and run, terminal | `MxcContainer.SpawnWithPty` -> `MxcPtyProcess` |
| Persistent container lifecycle | `MxcLifecycle.ProvisionContainer` / `StartContainer` / `RunInContainer*` / `SpawnInContainer*` / `StopContainer` / `DeprovisionContainer` |
| Dry-run validation | `MxcLifecycle.ValidateProvision` / `ValidateStart` / `ValidateProcess` / ... |
| Host discovery | `MxcPlatform.GetPlatformSupport()`, `MxcPlatform.GetAvailableBackends()`, `MxcContainer.Probe()` |
| Policy discovery helpers | `Policy.Filesystem.GetAvailableToolsPolicy()`, `GetUserProfilePolicy()`, `GetTemporaryFilesPolicy()` |

Behavioural details worth knowing before designing around it:

- `ContainerRequest` carries the workload plus `Filesystem`, `Network`, `UiPolicy`, `Containment`, and `TimeoutMs`.
  `UiPolicy.Disable` defaults to `true`.
- Cancelling `RunAsync` stops awaiting the result; native execution continues until completion or the request
  timeout. Timeouts, not cancellation tokens, bound execution.
- Environment omission uses backend defaults; an explicitly empty environment is preserved. Same distinction for
  the filesystem discovery helpers.
- `MxcPlatform.GetPlatformSupport()` availability is advisory; launch-time validation still applies.
- Native failures surface as `MxcException` with `Code`, `Operation`, `NativeCode`, and `Remediation`.
- Breaking changes to the published API require a new versioned surface, so V1 should be stable in practice.

## Policy model

- **Filesystem**: read-only, read-write, and denied path lists.
- **Network**: egress default allow/deny, proxy support, and backend-dependent host allow/deny filtering.
- **UI**: clipboard, display, and input injection.
- **Credentials**: not a policy dimension in MXC itself. Copilot layers a local proxy with masking hosts on top.

Enforcement is backend-dependent, and MXC fails closed on unsupported combinations rather than silently
weakening the request. That fail-closed validation is the property to rely on.

## Production precedent: GitHub Copilot local sandboxing

Copilot's local sandboxing is MXC. GitHub's documentation states it directly: "Local sandboxing is powered by
Microsoft eXecution Container (MXC)". Microsoft's own post confirms the mapping: Windows uses the
`BaseContainer` tier of the `ProcessContainer` backend, macOS uses Seatbelt, Linux uses bubblewrap, and none of
them require a VM or a container image.

GA: 2026-10-07, in Copilot CLI, the Copilot app, and VS Code agent sessions. Off by default.

### What Copilot sandboxes, and what it does not

Sandboxed:

- Shell commands the agent runs.
- Local MCP servers by default.
- Local language (LSP) servers.

Not sandboxed:

- The agent harness itself. Built-in first-party file tools run in-process in the CLI, so the OS sandbox never
  sees their file operations. They check the policy themselves, "on a best-effort basis".
- Remote MCP servers. Their connection policy is enforced in-process.

This is the single most important design fact for Symphony: the containment boundary is the process tree the
agent launches, not the agent.

### Controls Copilot exposes on top of MXC

- Filesystem read-only / read-write / denied paths.
- Outbound internet and local network access, plus host allow/deny rules. Local network access is on by default.
- Credential enablement for git and `gh`, plus masking-host configuration. Sandboxed tools receive placeholders
  and a local proxy substitutes real credentials only for approved HTTPS destinations.
- Whether local MCP and LSP servers run inside the sandbox.
- macOS keychain reachability.
- Per-command bypass prompts, with `sandbox.allowBypass` control.
- `sandbox.allowDevToolAccess` to grant package and build caches.
- Managed (enterprise) settings: `sandbox.enabled`, `sandbox.failIfUnavailable`, `sandbox.allowBypass`. When
  `sandbox.enabled` and `sandbox.failIfUnavailable` are both true, the CLI blocks model requests and tool
  execution if it cannot enforce sandboxing. On an unsupported host the saved preference is turned off for the
  session unless fail-if-unavailable applies.

### Copilot's Linux requirements

These are the real deployment constraints, per GitHub's documentation, whenever outbound traffic is permitted:

- `bubblewrap` >= 0.5.0 with `bwrap` on `PATH`.
- `slirp4netns` on `PATH`.
- `unshare` and `nsenter` from util-linux >= 2.35, with `--map-current-user` and `--keep-caps`.
- `iptables`, `ip6tables`, and their restore binaries, on the `nf_tables` backend. The legacy backend works only
  when `/run/xtables.lock` is writable.
- Access to `/dev/net/tun`.

And one hard limitation: on Linux, bubblewrap cannot control local network access independently for spawned
processes, including shell commands and local MCP/LSP servers. That setting still applies to in-process
operations.

macOS: Seatbelt, macOS 15 (Sequoia) or later. Windows: a recent Windows 11 build, with feature availability
(denied paths, localhost connections, proxying) varying by build. On Windows, host rules and proxying rely on
programs honouring the proxy, so they are weaker than the macOS/Linux path where the sandbox restricts egress to
the local proxy.

## What MXC is not

- It is not a VM or a container. Microsoft describes it as the lighter-weight end of the isolation spectrum: it
  restricts what a process can read, write, and reach, but runs in the same kernel.
- It does not contain the agent harness, remote MCP traffic, or anything the harness does in-process.
- It does not scope or rotate credentials. That is the caller's job; Copilot solves it with a credential proxy.
- It does not provide resource fairness (CPU/memory quotas are not a documented policy dimension).
- It does not protect against a malicious repository changing its own contents, or against prompt injection
  that produces legitimate-looking but harmful commits.
- It does not replace network egress control at the container/host layer, and on Linux it cannot promise
  per-process network denial.

## Symphony today: the surfaces that matter

| Surface | Implementation | Current containment |
| --- | --- | --- |
| Repository clone | `WorkspaceManager.CreateForIssueAsync` -> `git clone --depth 1 --single-branch --no-tags` | None; runs as the host user |
| `after_create` / `before_run` / `after_run` / `before_remove` hooks | `ShellCommandRunner` -> `cmd /c` or `bash -lc` | None; runs as the host user |
| Copilot agent | `CopilotAgent` via `GitHub.Copilot.SDK` | None; `OnPermissionRequest` returns `ApproveOnce` |
| Codex agent | `CodexAgent` -> `app-server` child process | Opt-in via `codex.thread_sandbox` / `turn_sandbox_policy` / `approval_policy` |

Supporting facts:

- The GitHub token reaches the agent process through `GitHubEnvironment.Build`, and git receives it as
  `http.https://github.com/.extraheader` rather than in the clone URL ([ADR-0003](../adr/0003-platform-owned-repository-checkout.md)).
  That keeps it out of `.git/config` but does not keep it out of child process memory or the environment.
- The published image runs as non-root UID/GID 1654 on `mcr.microsoft.com/dotnet/aspnet:10.0`, installs
  `git`, `gh`, `nodejs`, `npm`, `openssh-client`, `curl`, `jq`, `unzip`, and sets
  `SYMPHONY_WORKSPACE_ROOT=/home/app/workspaces`. It has no .NET SDK.
- Per-user workspace state, including `COPILOT_HOME`, lives under the workspace root. The web host's
  `App_Data` (SQLite database, Data Protection key ring) is in the same filesystem namespace as every agent
  process.
- `tests/Symphony.Tests` is unit-only: no browser, Docker, or external network
  ([ADR-0002](../adr/0002-test-boundaries-and-playwright.md)). Probe work therefore cannot live in CI.

Implication: the highest-risk surface is the auto-approved Copilot agent running repository code next to
`App_Data`, not the clone itself. Containment has to cover the agent's child processes, and it must not silently
degrade to "unsandboxed" on hosts where the backend is unavailable.

## Integration options

| Option | What it contains | Effort | Notes |
| --- | --- | --- | --- |
| A. Copilot CLI-native sandbox | Agent shell commands, local MCP, LSP | Low | Supported, GA feature of the runtime we already ship. Requires the SDK to surface sandbox config, or us driving the CLI |
| B. Embedded MXC for Symphony-owned shells | Clone, hooks, future user scripts | Medium | Direct `Microsoft.Mxc.Sdk` dependency, per-RID native assets, policy authoring |
| C. A + B | All of the above | Medium | Recommended shape, staged |

Option C matches Microsoft's own split and keeps the embedding surface small. A full "agent worker inside
MXC" (harness included) is rejected: it requires punching holes for model egress, auth, and `COPILOT_HOME`, and
still leaves the harness itself unchecked.

Open design questions:

1. Does `GitHub.Copilot.SDK` expose sandbox policy (paths, hosts, credential env vars), or only pass through
   `--sandbox` / `--no-sandbox`? If neither, driving the CLI ourselves is a fork in the design.
2. Do sandboxed Copilot shells inherit `GITHUB_TOKEN`? Changelog 1.0.92 says ambient tokens are withheld unless
   explicitly configured, which would break `git push` until we configure `credentials.envVars` or the proxy.
3. Which dev-tool grants does a Symphony job need in practice (`npm` cache, `~/.npmrc`, git and `gh` config,
   temp directory rename semantics)?
4. Does the probe pass inside the published image, and at what container privilege cost?

## Risks and traps

- **Credential withholding.** Enabling the sandbox can break `git push` and `gh` calls until credentials are
  explicitly configured. This must be covered by a conformance assertion, not discovered in production.
- **Long-tail dev-tool needs.** Agent command surfaces are broad. Expect "the agent cannot install X" failures
  and plan a user-extensible extra paths/hosts list.
- **Nested sandboxing.** The image is a stock non-root container. Bubblewrap there needs packages, `/dev/net/tun`,
  and network capabilities, i.e. a weaker outer container to enable a stronger inner one.
- **Audit asymmetry.** Denial capture exists on Windows but not Linux, so an audit-then-enforce rollout is
  Windows-only.
- **Native dependency risk.** The engine is native code loaded into the web host process. A fault in the engine
  is a fault in the host.
- **Version drift.** 1.0.x is weeks old. Pin the exact version and keep the integration behind one adapter.
- **Fail-open drift.** The dangerous outcome is a job that runs unsandboxed because the host could not enforce
  the policy. Any admin switch must support a fail-closed mode with a typed job error.

## Proposed shape (pending probe)

1. Admin setting with three states: `Off`, `On (best effort)`, `Required (fail closed)`. Show the probe result and
   the effective policy in the UI.
2. Enable the Copilot CLI's native sandbox under that setting. Codex already has its own sandbox configuration and
   is out of scope for this work.
3. Wrap Symphony-owned clone and hook shells in MXC directly.
4. Roll out audit -> enforce on Windows; enforce directly where audit is unavailable, gated by the probe.
5. Keep container-per-job as the server-side isolation boundary until the in-container probe proves viable.

## Sources

- <https://github.com/microsoft/mxc> - README, backends, telemetry, diagnostics.
- <https://github.com/microsoft/mxc/blob/main/docs/backends/process-container/os-version-support.md> - Windows build gates.
- <https://github.com/microsoft/mxc/blob/main/docs/api-reference/README.md> - V1 API reference index.
- <https://www.nuget.org/packages/Microsoft.Mxc.Sdk> - package metadata and .NET sample.
- <https://docs.github.com/copilot/concepts/about-cloud-and-local-sandboxes> - Copilot local sandboxing, what is
  and is not sandboxed, cross-platform requirements, managed settings.
- <https://commandline.microsoft.com/local-models-sandboxed-tools-github-windows/> - MXC-to-Copilot backend mapping
  and the process-boundary description.
- <https://github.blog/changelog/2026-10-07-local-sandboxing-for-github-copilot-now-generally-available/> - GA date.
- <https://github.com/github/copilot-cli/blob/main/changelog.md> - `/sandbox`, credential masking, dev-tool grants,
  token withholding, proxy behaviour.
- <https://github.com/github/copilot-cli/issues/3653> - reported Linux local-sandbox failure.
