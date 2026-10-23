# T8: Application Dockerfile and development compose

## Problem

Symphony has no container image or local compose stack. Operators cannot run the web host in a container, and there is no source-based local development environment with PostgreSQL. The app must build as a multi-stage .NET image, expose 8080, run as non-root, and print first-admin credentials to container logs. Schema must be created automatically on startup (already implemented via `DatabaseInitializer.InitializeAsync`).

## Proposed changes

- Create `src/Symphony/Dockerfile` (multi-stage: `dotnet/sdk:10.0` build, `dotnet/aspnet:10.0` runtime).
  - Restore and publish `src/Symphony/Symphony.csproj` for `linux-x64`, `--no-self-contained`, output `/app`.
  - Create `/app/App_Data` and `/home/app/.aspnet/DataProtection-Keys`, chown to `app`.
  - Run as `app` (non-root), expose 8080, set `ASPNETCORE_URLS=http://+:8080`.
  - Entrypoint `dotnet Symphony.dll`.
- Create root `.dockerignore` excluding build artifacts, tests, docs, local data.
- Create root `docker-compose.yml` for source-based local dev:
  - PostgreSQL 16 with health check and named volume.
  - App service building from repo root context with Dockerfile.
  - Provider-switching env vars (`Database__Provider=postgresql`, `ConnectionStrings__PostgreSQL`).
  - Volumes for app data and Data Protection keys.
  - App health check (TCP probe) and `depends_on` condition.

No changes to `Program.cs` for T8: port binding via env var; Data Protection persistence deferred to T11 (default key ring path mounted).

## Blast radius

- New files only: `src/Symphony/Dockerfile`, `.dockerignore`, `docker-compose.yml`, `docs/TODO.md`.
- No C# code changes; existing tests unaffected.
- Build context is repository root; `.dockerignore` excludes `tests/`, `docs/`, `bin/`, `obj/`.

## Verification steps

- `docker build -f src/Symphony/Dockerfile .` succeeds.
- `docker compose up` starts PostgreSQL and app; schema auto-created.
- First-admin credentials print to container logs once.
- Login works at http://localhost:8080.
- `dotnet build Symphony.slnx` still passes.

## Planned commits

1. `docs: plan T8 Dockerfile and compose in TODO.md`
2. `feat: add multi-stage Dockerfile for Symphony web app`
3. `chore: add .dockerignore for container builds`
4. `feat: add root docker-compose for local PostgreSQL development`
5. `docs: remove TODO.md — plan executed`

## GitHub issues log

- (none yet)

## Notes

- Data Protection volume path must stay aligned with T11 when implemented.
- Compose uses sample-only default credentials; override via env for real deployments.
