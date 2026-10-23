# Plan: T8A CI workflow + T8B Docker build/publish (issues #14, #15)

## Problem

- #14 (T8A): CI workflow needs to build/test only `tests/Symphony.Tests`, collect Cobertura coverage via `coverlet.collector`, fail if no coverage file, override environment to Production. Do not touch `release-readiness.yml` or `cd.yml`.
- #15 (T8B): Publish web app image to GHCR. Replace build-only `cd.yml` with a publish workflow; add manual `docker.yml`. Document publish path. Do not touch `ci.yml`/`release-readiness.yml`.

## Proposed changes

Commit 1 — #14 CI:
- `tests/Symphony.Tests/Symphony.Tests.csproj`: add `coverlet.collector` 6.0.4 (`PrivateAssets="all"`).
- `.github/workflows/ci.yml`: job-level `ASPNETCORE_ENVIRONMENT: Production`; keep project-scoped build/test + filter `Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness`; keep `--collect:"XPlat Code Coverage"`; add "Verify coverage file" step; keep uploads.
- `README.md` (Development): point at `dotnet test tests/Symphony.Tests`, `ci.yml`, probes/perf not CI, Cobertura via coverlet.
- `AGENTS.md` (Build & Verify): filter + coverage expectation.

Commit 2 — #15 publish:
- `.github/workflows/cd.yml`: replace with publish workflow (tag + manual dispatch `version`, GHCR login, build-push `:<version>` + `:latest`).
- `.github/workflows/docker.yml`: new manual publish workflow (`image_tag` default `latest`).
- `README.md`: release image publish path + image name.
- `AGENTS.md`: `cd.yml` is publish workflow (not build-only); `docker.yml` manual.

## Verification

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (ask before running)
- YAML sanity: parse workflows (ruby/python or visual)
- Docker build locally: `docker build -f src/Symphony/Dockerfile .`

## Blast radius

- Workflows: `.github/workflows/{ci,cd,docker}.yml`.
- Test project: `tests/Symphony.Tests/Symphony.Tests.csproj`.
- Docs: `README.md`, `AGENTS.md`.
- No source code changes; no test logic changes. Low risk.

## GitHub issues log

- #14 — T8A: CI workflow (build and test)
- #15 — T8B: Docker build and publish workflow (GHCR)

## Grounding

- Code-memory MCP used to navigate repo symbols (DatabaseRegistration, Program, etc.).
- CodeMemory repo (E:\khurram-uworx\CodeMemory) used as reference for workflow/docker patterns.
- Microsoft-learn not applicable (GitHub Actions / Docker, not .NET API).
- Plan cleared by human via "implement the plan".