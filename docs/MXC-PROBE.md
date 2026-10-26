# MXC probe plan

Status: proposed. Owner: TBD. Tracking issue: [#118](https://github.com/khurram-uworx/Symphony/issues/118).
Depends on [research/MXC.md](research/MXC.md).

Purpose: prove, with evidence, whether Symphony can contain agent execution with MXC on the hosts we ship to -
especially inside the published Linux container - before committing to an ADR.

The ADR (`docs/adr/0005-*`) is written only after this probe reaches its exit criteria. If the probe fails, the
ADR is still written, but it records the failure and adopts the fallback instead.

## What "probe confirmed" means

The probe is confirmed when all of the following are true and recorded as evidence:

1. Capability is detected correctly on every target host, and an unsupported host is reported as unsupported
   rather than silently treated as sandboxed.
2. A deny-listed read (Symphony `App_Data`, another user's workspace) fails inside the sandbox.
3. A legitimate repository workflow succeeds inside the sandbox: `npm install`, a test command, and a GitHub push
   using the job token.
4. A malicious fixture is stopped and returns an actionable error (code + remediation), not a bare exit code.
5. A runaway command is killed at the timeout with no orphaned children.
6. The Linux container case works, or fails in a way we can state precisely: which minimal flags, which image
   changes, and what is lost in the outer container.
7. The Copilot SDK path is settled: either the SDK exposes sandbox configuration, or driving the CLI directly is
   accepted as the integration mechanism.
8. Overhead is measured and judged acceptable for a per-command sandbox.

Anything short of that is a partial result. Partial results are still useful; they choose the fallback.

## Non-goals

- No production code changes. The probe is throwaway harness code plus fixtures.
- No CI integration. `tests/Symphony.Tests` stays unit-only ([ADR-0002](adr/0002-test-boundaries-and-playwright.md)).
- No policy tuning for every language ecosystem. Prove Node/npm and one deny case; document the long tail.
- No Codex work. `CodexAgent` is not on the hot path and is out of scope here; it already has its own
  `thread_sandbox` / `turn_sandbox_policy` configuration and can be revisited separately.
- No decision about cloud sandboxes or remote execution.

## Layout

A probe project alongside the existing manual harnesses, never referenced by CI:

```
tests/Symphony.MxcProbe/          console app, net10.0, Microsoft.Mxc.Sdk pinned
tests/Symphony.MxcProbe/fixtures/ conformance repositories (checked in or generated)
docs/research/MXC-PROBE-RESULTS.md  evidence, filled in as stages complete
```

`tests/Symphony.MxcProbe` mirrors `tests/Symphony.CodingAgentsProbe`: run manually, excluded from the solution's
default test path, not part of `dotnet test`.

## Test matrix

| Host | Backend | Why |
| --- | --- | --- |
| Windows 11 dev workstation | `processcontainer` | Primary developer host; Copilot's Windows path |
| macOS 15+ | `seatbelt` | Secondary developer host; strongest network enforcement |
| Linux host (bare) | `bubblewrap` | Reference for the container path |
| `aspnet:10.0` container, non-root 1654 | `bubblewrap` | The published deployment | 
| `ghcr.io/khurram-uworx/symphony:latest` | `bubblewrap` | The actual artifact users run |

Record the observed OS build on Windows, `bwrap --version`, and the exact image digest for each run.

## Stage 1 - capability inventory

Goal: know what the host says before trusting it.

- Windows: check the build against the ProcessContainer minimums (24H2 needs 26100.9278 or later). Call
  `MxcPlatform.GetPlatformSupport()`, `MxcPlatform.GetAvailableBackends()`, and `MxcContainer.Probe()` with the
  intended request, and record the tier it selects plus all warnings.
- macOS/Linux: `GetPlatformSupport()`, plus the dependency probes that Copilot's documentation lists.
- Container: the above, plus explicit presence checks for `bwrap`, `slirp4netns`, `unshare`, `nsenter`,
  `iptables`, `iptables-restore`, `ip6tables`, `ip6tables-restore`, and `/dev/net/tun`.

Evidence: a capability table per host, pasted into the results file.

## Stage 2 - policy self-test

Goal: prove the policy does what we think, independent of any agent.

Build the probe app around one `ContainerRequest` per case, with the workspace as the only read-write path and the
toolchain as read-only. Cases:

| Case | Expectation |
| --- | --- |
| Write a file in the workspace | Allowed |
| Read a file in the workspace | Allowed |
| Read the Symphony database path (`App_Data/symphony.db`) | Denied |
| Read another user's workspace | Denied |
| Read the Data Protection key ring | Denied |
| Connect to a deny-listed host | Denied on macOS/Linux. On Windows record whether host rules enforced, and mark as advisory |
| Connect to an allow-listed host (GitHub API) | Allowed |
| Resolve a non-allowlisted DNS name | Record behaviour; DNS can bypass naive host rules |
| Spawn a child process that attempts a denied read | Denied |
| Runaway loop with `TimeoutMs` set | Killed at timeout, no surviving children |
| Spawn with `SpawnAsync` and stream output | Works; cancellation does not leave native execution orphaned |

Additional checks:

- An explicitly empty environment is preserved; an omitted environment uses backend defaults.
- `MxcException` carries a usable `Code` and `Remediation` for each denial.
- On Windows, run `captureDenials` in record mode and confirm a denial is recorded without failing the command.

Evidence: one row per case per host, with the raw error code.

## Stage 3 - conformance fixture repository

Goal: prove an agent-shaped workload works, and that abuse inside it does not.

Create a small repository with:

- `package.json` with a `postinstall` that writes a marker file into the workspace (legitimate) and a second
  fixture variant whose `postinstall` tries to read `App_Data`, read a sibling workspace, and POST to a
  non-allowlisted host (abuse).
- A local stdio MCP server (a few lines of Node) that the agent can list tools from.
- A test script that writes to `node_modules` and a build cache, to exercise dev-tool grants.
- A `git` flow that commits locally and runs `git push --dry-run` against the job repository using the token.

Expected results:

- Legitimate variant: install, test, and push dry-run all succeed inside the sandbox.
- Abuse variant: the command fails, the marker inside the workspace is the only side effect, nothing outside the
  workspace is touched, and the failure is reported with a code.
- With the sandbox disabled, both variants run, which demonstrates the fixture is meaningful.

This stage is also the seed of the future "sandbox conformance" feature: the same fixture, run from the admin UI
before switching the setting to `Required`.

## Stage 4 - agent in the loop (Copilot SDK only)

Goal: settle how the Copilot agent's own child processes get contained. This is the stage that decides the
integration shape, because Symphony does not own the Copilot CLI's shell tool.

1. Determine whether `GitHub.Copilot.SDK` surfaces sandbox configuration. Options to check, in order: session
   config properties, `--sandbox` / `--no-sandbox` pass-through, environment or settings-file driven
   configuration, or driving the CLI directly with our own stdio bridge.
2. With the sandbox on, run a real job against the fixture repository and verify:
   - `git push` works (watch for ambient `GITHUB_TOKEN` withholding).
   - `gh` works, or its absence is documented.
   - Local MCP servers start inside the sandbox. Remote MCP (Linear) must remain unaffected.
   - `COPILOT_HOME` under the workspace is readable and writable.
   - The telemetry OTLP receiver (loopback) is not needed by any sandboxed process. It should not be, because the
     harness is outside the sandbox, but verify.
3. Record which of these were reachable from configuration alone, and which would require bypassing the SDK.

Evidence: a working job with the sandbox on, plus a diff of what the agent could and could not do compared with
the unsandboxed run.

Codex is deliberately excluded. It is not on the hot path, and its containment is a configuration pass-through
we can revisit on its own schedule.

## Stage 5 - container probe (the decisive stage)

Goal: find the minimal privilege and image cost of running the Linux backend inside the published image.

Start from the current image and add requirements in steps, recording what each step unlocks:

```bash
# 0. baseline, expect failure
docker run --rm -it --user 1654:1654 ghcr.io/khurram-uworx/symphony:latest bash -lc 'bwrap --version'

# 1. install the backend dependencies
docker run --rm -it --user root ghcr.io/khurram-uworx/symphony:latest bash -lc \
  'apt-get update && apt-get install -y --no-install-recommends bubblewrap slirp4netns iptables util-linux && bwrap --version'

# 2. add the device and network capability
docker run --rm -it --user 1654:1654 --device /dev/net/tun --cap-add NET_ADMIN \
  -v "$PWD/tests/Symphony.MxcProbe:/probe" ghcr.io/khurram-uworx/symphony:latest \
  bash -lc 'ls -l /dev/net/tun && bwrap --unshare-all --ro-bind / / true'

# 3. relax seccomp only if step 2 fails, and record why
docker run --rm -it --user 1654:1654 --device /dev/net/tun --cap-add NET_ADMIN \
  --security-opt seccomp=unconfined ... 
```

Record for each step: does a trivial sandboxed command run, does egress-to-allowlist work, does deny work, and
which capability is load-bearing. Then re-run Stages 1-4 inside the working container configuration.

Known constraints to confirm rather than assume:

- Linux bubblewrap cannot control local-network access independently for spawned processes; expect the deny case
  for loopback/private ranges to be unenforceable.
- Debian-based images may gate unprivileged user namespaces
  (`/proc/sys/kernel/unprivileged_userns_clone`, and on Ubuntu `kernel.apparmor_restrict_unprivileged_userns`).
- `--cap-add NET_ADMIN` plus `--device /dev/net/tun` materially weakens the outer container. That trade-off is a
  decision input for the ADR, not a detail to bury.
- The image has no .NET SDK, so the probe app must be published `linux-x64` from the build stage or run as a
  separate container against the same volume.

Exit states for this stage, in order of preference:

- **Confirmed**: sandbox works in-container with the documented minimal flags and no loss of integrity for
  legitimate jobs.
- **Conditional**: works only with flags we consider unacceptable for the default image. Then MXC is a desktop
  feature and the server keeps container-per-job isolation.
- **Blocked**: does not work in-container at all. Then the same fallback applies, with the reason recorded.

## Stage 6 - soak and rollout rehearsal

Goal: find the long tail before users do.

- Run 50 consecutive jobs against the fixture repo with the sandbox on. Check for leaked processes, leaked
  mounts, workspace corruption, and probe latency drift.
- Run on Windows in denial-record mode for a realistic working session and collect the paths and hosts that were
  actually needed. This produces the first real policy instead of a guess.
- Measure per-command overhead against the unsandboxed baseline and record p50/p95.

## Acceptance record

Fill in [research/MXC-PROBE-RESULTS.md](research/MXC-PROBE-RESULTS.md) with:

| Field | Meaning |
| --- | --- |
| Host | OS, build, backend, image digest |
| Capability | `GetPlatformSupport` result and selected tier |
| Policy cases | Pass/fail per Stage 2 row, with error codes |
| Conformance | Legitimate variant result, abuse variant result |
| Agent | Copilot sandbox-on result, including push and MCP |
| Container | Minimal flags, image changes, what was lost |
| Overhead | p50/p95 per command vs baseline |
| Verdict | Confirmed / conditional / blocked, with the fallback chosen |

## Fallbacks, in order

1. **Option C as researched**: Copilot-native sandbox for the agent's child processes, plus MXC for
   Symphony-owned shells, on desktop and in the container where the probe allows it.
2. **Split by deployment**: MXC on Windows/macOS developer hosts; container-per-job isolation on the server.
3. **Container-only hardening**: no MXC; narrower worker container, default-deny egress, scoped per-job tokens.

## Risks to the probe itself

- Testing with a hand-written `ContainerRequest` can pass while the real agent policy fails. Stage 4 exists to
  close that gap; do not skip it.
- Windows host coverage is easy to over-generalise. A pass on one build does not mean the same tier is available
  on a colleague's machine or on a CI runner.
- Probe code that becomes production code without review. Keep it in `tests/`, behind an explicit command, and do
  not reference it from `src/`.

## Checklist

- [ ] Probe project scaffolded and pinned to one `Microsoft.Mxc.Sdk` version
- [ ] Stage 1 capability table recorded for all five matrix rows
- [ ] Stage 2 policy self-test green (or documented as unenforceable per host)
- [ ] Stage 3 fixture repository with legitimate and abuse variants
- [ ] Stage 4 Copilot sandbox question settled and a job proven end to end
- [ ] Stage 5 container minimal-flag set established and re-run green
- [ ] Stage 6 soak and denial-record session complete
- [ ] `docs/research/MXC-PROBE-RESULTS.md` filled in
- [ ] ADR-0005 drafted from the verdict
