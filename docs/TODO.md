# Plan: close issue #68 — non-root container is verified, document it and guard it

Branch: `khurram/68`
Issue: [#68](https://github.com/khurram-uworx/Symphony/issues/68) — "Container image fails to start as non-root user: /app/App_Data permission denied"

## Problem

Issue #68 reported `UnauthorizedAccessException: Access to the path '/app/App_Data' is denied` when the
container runs as the built-in non-root `app` user (UID 1654).

Verification on the current base already shows the supported image is healthy:

- `src/Symphony/Dockerfile` creates `/app/App_Data`, `/home/app/.aspnet/DataProtection-Keys`, and
  `/home/app/workspaces`, `chown`s them to `app`, and ends with `USER app`.
- A rebuilt image starts as UID 1654, creates `symphony.db` and the Data Protection key ring under
  `/app/App_Data`, and serves HTTP (`GET /` returns the normal 302).
- The published `ghcr.io/khurram-uworx/symphony:latest` behaves the same.

So there is no application or image defect left to fix on that path. Two real gaps remain:

1. **Docs gap** — `CONTAINER-README.md` lists only `/home/app/...` as pre-created, omits `/app/App_Data`
   and the UID, and never says the Dockerfile is the supported build path. The issue's exact repro
   (`dotnet publish /t:PublishContainer`) generates a different image with no writable `App_Data`, and
   that limitation is undocumented.
2. **No regression guard** — no workflow ever runs the image as non-root, so a future Dockerfile edit
   could silently reintroduce #68.

## Proposed changes

### 1. Document non-root invariants and the supported build path

`CONTAINER-README.md`:

- Replace the one-liner at line 67 with the concrete non-root contract: image runs as `app` (UID 1654),
  pre-creates `/app/App_Data`, `/home/app/.aspnet/DataProtection-Keys`, `/home/app/workspaces`, all
  owned by `app`; named volumes inherit ownership, bind mounts do not (must be `chown`ed); `-u 0` is
  neither required nor supported.
- Add an "Image build path" note: the Dockerfile at `src/Symphony/Dockerfile` is what CI/CD, compose,
  and the published image use. `dotnet publish /t:PublishContainer` builds an SDK-generated image that
  has no writable data directory and no agent toolchain, so running it as non-root is not supported.
- Mention `/app/App_Data` as the persistence volume when SQLite is used.

### 2. Non-root container smoke in Release Readiness

`.github/workflows/release-readiness.yml` — add a second job (parallel to the existing test job):

- Build the image from `src/Symphony/Dockerfile` (`docker build -t symphony:ci -f ... .`).
- Fail if the image's `Config.User` is not `app`.
- Run `--user app` and assert `id -u` is not 0 and that a file can be written to `/app/App_Data`.
- Start the image as `app`, wait for "Application started", assert `/app/App_Data/symphony.db` exists,
  assert `GET /` returns a sub-500 status, then remove the container.
- Always clean up the container (`if: always()`), so a failed smoke does not leak containers.

### 3. Keep the issue linked

PR description uses `Closes #68` so merging closes the issue.

## Verification

1. `docker build -f src/Symphony/Dockerfile -t symphony:68 .` (fresh build, no stale-image assumptions).
2. Run the new workflow commands locally against that image: user is `app`, `--user app` can write
   `/app/App_Data`, container boots, SQLite file exists, `GET /` responds, container cleaned up.
3. Playwright MCP against the running non-root container on `http://localhost:8080`: sign in as the
   seeded admin, confirm the home page renders (proves the non-root runtime serves real traffic).
4. `dotnet build Symphony.slnx` (no C# changes, but proves the tree still builds).
5. `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
   (ask the human before running).
6. Workflow YAML sanity: inspect the rendered file, confirm job names/steps parse and that the smoke
   uses only tools present on `ubuntu-latest` and in the image (`curl`, `sh`).

## Planned commits

1. `docs: plan issue 68 verification and container hardening in TODO.md`
2. `docs(container): document non-root runtime invariants`
3. `ci: smoke the container as non-root in release readiness`

## Blast radius

- `CONTAINER-README.md` — documentation only; no runtime behavior.
- `.github/workflows/release-readiness.yml` — adds one nightly job; existing `release-readiness` job
  unchanged. Costs one image build per run (~4 minutes) and can fail the nightly run if non-root
  startup regresses, which is the point.
- No application code, migrations, config defaults, or tests change. `Symphony.Tests` untouched.

## GitHub issues log

None created yet. Record any issue raised while executing here.

- [ ] #68 — closed by this branch (PR uses `Closes #68`).
