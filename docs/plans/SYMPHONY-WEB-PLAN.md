# Symphony Web — Task Breakdown

Derived from `docs/plans/SYMPHONY-WEB.md` and structured using `docs/TASKS-TEMPLATE.md`. This document is execution-ready: each task is sized for one coding agent, includes acceptance criteria, and lists likely files.

This is a planning document only. No implementation is started by this file.

## Milestone capability map

| Milestone | Capability unlocked after completing listed tasks |
|---|---|
| M1 | Web app runs; console prints one-time admin credentials; admin can log in |
| M2 | Docker + PostgreSQL compose works (dev and published-image sample); GHCR image is published via CI/CD; admins can create member accounts; admins and members can log in and are greeted |
| M3 | Users save configuration and workflows; work can be submitted from the web/API and queued |
| M4 | Symphony can fetch Jira ticket context and complete its own first Jira-ticket job |
| M5 | Symphony can run a job against its own GitHub repository using a supplied PAT |
| M6 | A terminal `curl` submission against an arbitrary configured repository/ticket succeeds |
| M7 | Admin controls, recovery, live updates, settings, and provider tests are in place |
| M8 | First-cut web product is hardened, documented, and verified end to end |

## Purpose

This document breaks the Symphony Web reimplementation into concrete, assignable tasks for coding agents. Keep each task small enough that one agent can own it end-to-end. Decision-gate tasks are marked explicitly.

## How to use

- Work milestones in order for the first pass: M1, M2, ..., M8.
- Within a milestone, follow the suggested task order.
- Respect decision-gate notes before starting dependent tasks.
- Use the acceptance criteria as the handoff checklist.
- Treat files listed as "likely involved" as the starting search set, not an exclusive list.

## Suggested execution order (milestone order)

1. M1: T1 -> T5
2. M2: T6 -> T11
3. M3: T12 -> T24
4. M4: T25 -> T34
5. M5: T35 -> T38
6. M6: T39 -> T43
7. M7: T44 -> T49
8. M8: T50 -> T52

## Coordination notes

- Decision-gate tasks: T1 (entry point removal), T6 (provider switching), T19 (secret protection), T20 (config factory shape), T25 (`IServiceConfigProvider`), T35 (PAT handling), T44 (retry identity), T51 (legacy polling removal).
- Tasks that should not begin until earlier gates settle:
  - T12/T13 depend on T19.
  - T20 depends on T12/T13/T15/T19.
  - T22/T23 depend on T20/T21.
  - T29 depends on T20/T25/T26/T27/T28.
  - T31/T35 depend on T29.
  - T39 may start after T9; it is required before M6 verification.
- Parallel work is safe:
  - Within M3, T15-T18 (workflows) and T12-T14 (configuration) can progress in parallel after T19.
  - UI tasks (T9, T10, T14, T17, T23, T33, T41, T47) can proceed in parallel once their backing APIs/entities are merged.
  - Verification tasks (T5, T34, T38, T43, T52) can be prepared early and executed after implementation.
  - Test-hardening tasks (T40, T49) can run alongside feature work if interfaces are stable.
- Shared files with high merge-conflict risk:
  - `src/Symphony.App/Symphony.App.csproj`
  - `src/Symphony.App/Program.cs`
  - `src/Symphony.App/Data/SymphonyDbContext.cs`
  - `src/Symphony.App/appsettings*.json`
  - `src/Symphony.App/Middleware/*`
  - `tests/Symphony.Tests/Symphony.Tests.csproj`
- Open decisions still to be settled (see `docs/plans/SYMPHONY-WEB.md` section 15a):
  - Optional per-request override allow-list.
  - Workflow identity vs. mutability (rename cascade, name vs. ID).
  - Workspace root safety/validation.

## Suggested agent handout batches

### Batch A: decision-critical

- T1, T6, T19, T20, T25, T35, T44, T51

### Batch B: implementation

- T2-T4, T7-T18, T21-T33, T36-T42, T45-T48, T50

### Batch C: tests, verification, docs

- T5, T34, T38, T40, T43, T49, T52

---

## Task T1: Convert app project to ASP.NET Core web application

### Priority

High

### Milestone

M1

### Goal

Convert `Symphony.App` from a console host to a web host that can serve HTTP requests and run hosted services.

### Why this exists

The web application is the product. The file/workflow polling entry point is removed rather than retained as legacy mode.

### Decision required

Confirm that the `symphony --workflow WORKFLOW.md` entry point is removed and that configuration comes from `appsettings`/environment.

### Scope

- Change SDK to `Microsoft.NET.Sdk.Web` in `src/Symphony.App/Symphony.App.csproj`.
- Keep `net10.0` and existing runtime packages.
- Replace `WorkflowPathResolver.Resolve(args)` startup with a `WebApplication`/`Host` builder.
- Add minimal routing and a placeholder landing page or health response.
- Preserve existing DI registrations that remain valid, but remove workflow-path-specific startup.

### Constraints

- Do not retain candidate polling as the shipped path.
- Keep build green for tests until removed references are updated.

### Suggested implementation path

- Update csproj SDK and output.
- Replace `Program.cs` try/catch console host with web builder.
- Move hosted service registration later; first get app listening.

### Docs and workflows

Update these in the same change. Do not leave the console poller documented as the product.

- `README.md`: Getting Started and Running must start the web host. Do not document `dotnet run -- path/to/WORKFLOW.md` as how to run Symphony.
- `AGENTS.md`: project overview and repository map must describe the web host as the entry point. Legacy polling removal remains T51; do not document it as the way to run the app.

Do not edit GitHub workflows in T1.

### Acceptance criteria

- `dotnet build Symphony.slnx` succeeds.
- `dotnet run --project src/Symphony.App` starts Kestrel.
- Navigating to `/` returns a valid response.
- No `WorkflowPath` is required to start.
- `README.md` and `AGENTS.md` no longer tell an operator to start the product with a workflow file.

### Files likely involved

- `src/Symphony.App/Symphony.App.csproj`
- `src/Symphony.App/Program.cs`
- `src/Symphony.App/WorkflowPathResolver.cs`
- `README.md`
- `AGENTS.md`

---

## Task T2: Add Identity and initial database bootstrap (SQLite)

### Priority

High

### Milestone

M1

### Goal

Introduce ASP.NET Core Identity on SQLite with a `SymphonyDbContext`, provider wiring, and startup schema creation.

### Why this exists

Multi-user authentication and roles are foundational. SQLite is the default local provider.

### Scope

- Add packages: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`.
- Create `Data/SymphonyDbContext.cs` deriving from `IdentityDbContext`.
- Register DbContext factory with provider selection at least for SQLite.
- Create SQLite parent directory before `EnsureCreated`/migrations.
- Run schema initialization during startup.
- Configure cookie authentication defaults.

### Constraints

- Provider choice should come from `Database:Provider`, not be hard-coded.
- Do not seed users yet.

### Suggested implementation path

- Create DbContext with Identity entities only.
- Add `EnsureCreated` for development; keep migration extension points.
- Register `AddDbContextFactory`.

### Acceptance criteria

- App starts with `Database:Provider=sqlite`.
- `App_Data/symphony.db` is created when absent.
- Identity tables exist in SQLite.
- No startup exception when database directory is missing.

### Files likely involved

- `src/Symphony.App/Symphony.App.csproj`
- `src/Symphony.App/Data/SymphonyDbContext.cs`
- `src/Symphony.App/Program.cs`
- `src/Symphony.App/appsettings.json`
- `src/Symphony.App/appsettings.Development.json`

---

## Task T3: First-admin bootstrap and console credential output

### Priority

High

### Milestone

M1

### Goal

Seed the first admin user only when no admin exists, generate a random password, and print it exactly once.

### Why this exists

There is no public registration. The first admin must bootstrap access.

### Scope

- Seed `Admin` and `User` roles.
- If no user has the `Admin` role:
  - create username from `SeedAdmin:UserName` (default `testadmin`)
  - generate a cryptographically random password
  - print username/password to console once
- If any admin exists, skip seeding and avoid printing credentials.
- Make seeding idempotent.

### Constraints

- Never overwrite or print existing credentials.
- Print to console only, not logs on every boot.

### Suggested implementation path

- Implement a hosted startup seeder or `IHostedService`.
- Guard seeding in a transaction.
- Store only password hash via Identity.

### Acceptance criteria

- First boot prints a username/password pair once.
- Subsequent boots do not print credentials.
- Restarting after first boot preserves admin account.
- Seeded admin is in `Admin` role.

### Files likely involved

- `src/Symphony.App/Identity/AdminSeeder.cs`
- `src/Symphony.App/Program.cs`
- `src/Symphony.App/Data/SymphonyDbContext.cs`

---

## Task T4: Minimal authenticated web shell and greeting

### Priority

High

### Milestone

M1

### Goal

Provide a minimal Razor Pages UI where an authenticated user can log in and see a personalized greeting.

### Why this exists

M1 acceptance requires seeing a web app and logging in with the printed password.

### Scope

- Add Identity UI or custom login page.
- Add an authenticated page (for example `/` or `/dashboard`) showing `Hello, {UserName}`.
- Add role-protected `/admin` area placeholder.
- Add basic navigation/layout.

### Constraints

- Follow `.editorconfig` and existing code style.
- Role `Admin` required for `/admin/*`.

### Suggested implementation path

- Use Razor Pages.
- Configure default cookie redirect to login.
- Keep styling minimal; functional first.

### Acceptance criteria

- Login page renders.
- Admin logs in with console-printed credentials.
- Authenticated page greets the signed-in user by name.
- Unauthenticated access to protected pages redirects to login.
- Logout works.

### Files likely involved

- `src/Symphony.App/Pages/Index.cshtml`
- `src/Symphony.App/Pages/Shared/_Layout.cshtml`
- `src/Symphony.App/Pages/Admin/Index.cshtml`
- `src/Symphony.App/Program.cs`

---

## Task T5: Web smoke test baseline

### Priority

Medium

### Milestone

M1

### Goal

Add a basic integration test baseline using `WebApplicationFactory<Program>`.

### Why this exists

Gives agents a fast regression check for web startup and login flow.

### Scope

- Add test dependencies for ASP.NET Core testing and SQLite temp database.
- Add a test that app starts and `/` returns success.
- Add a test that the seeded admin can sign in and reaches the greeting page.
- Configure tests to use an isolated temporary SQLite file.

### Constraints

- Do not depend on real network or external services.
- Keep tests runnable with `dotnet test`.

### Suggested implementation path

- Add `Microsoft.AspNetCore.Mvc.Testing` to tests.
- Use `WebApplicationFactory` with Testing configuration override.
- Override connection string/database provider.

### Docs and workflows

The nightly Release Readiness workflow already filters `Category=ReleaseReadiness` and currently matches nothing. T5 is the task that makes that gate real.

- Mark the startup and admin sign-in smoke tests `[Category("ReleaseReadiness")]` in addition to any smoke category.
- Tests live in `tests/Symphony.Tests`, not `src/Symphony.Tests`.
- Update `AGENTS.md` testing conventions with the `ReleaseReadiness` category and the test project path.
- Do not edit workflow files here. T8A owns `.github/workflows/`.

### Acceptance criteria

- `dotnet test tests/Symphony.Tests` runs new smoke tests.
- Tests pass on a clean checkout.
- Tests do not leave database files after completion, or use disposable temp paths.
- `dotnet test tests/Symphony.Tests --filter Category=ReleaseReadiness` runs at least the startup and admin sign-in tests.

### Files likely involved

- `tests/Symphony.Tests/Symphony.Tests.csproj`
- `tests/Symphony.Tests/WebSmokeTests.cs`
- `src/Symphony.App/Program.cs`
- `AGENTS.md`

---

## Task T6: Multi-provider EF Core registration and configuration

### Priority

High

### Milestone

M2

### Goal

Support SQLite, SQL Server, and PostgreSQL providers through configuration, following the provider-switching pattern.

### Decision required

Confirm connection string keys and aliases: `Sqlite`, `SqlServer`, `PostgreSQL`/`Npgsql`.

### Scope

- Add EF packages for SQL Server and Npgsql.
- Implement provider switch from `Database:Provider`.
- Register `AddDbContextFactory<SymphonyDbContext>`.
- Include connection string aliases and clear validation errors.
- Production connection settings documentation in config.

### Constraints

- Do not hard-code a provider.
- Provider-specific migrations are separate; this task handles registration and validation.

### Suggested implementation path

- Mirror the switch example in `docs/plans/SYMPHONY-WEB.md`.
- Throw helpful errors on missing connection strings.
- Normalize aliases `postgres`/`npgsql`.

### Acceptance criteria

- App starts with `sqlite`, `sqlserver`, and `postgresql` given valid connection strings.
- Missing required connection strings produce a clear startup error.
- Config-only switching changes the active provider.

### Files likely involved

- `src/Symphony.App/Symphony.App.csproj`
- `src/Symphony.App/Program.cs`
- `src/Symphony.App/appsettings.json`
- `src/Symphony.App/Data/SymphonyDbContext.cs`

---

## Task T7: Schema initialization and seed data

### Priority

High

### Milestone

M2

### Goal

Initialize schema according to provider and seed roles, first admin, default settings, and workflows.

### Scope

- Create SQLite directory when needed.
- Run `EnsureCreated` in development or migrations in production.
- Run provider-specific migrations where required.
- Seed roles after schema init.
- Seed first admin via T3.
- Seed default `AppSetting` values.

### Constraints

- Do not assume one migration set is portable.
- Seed only when no admin role exists.
- Provider-specific features must stay isolated.

### Suggested implementation path

- Add `DatabaseInitializer` startup component.
- Branch on provider and environment.
- Keep seed transactional/idempotent.

### Acceptance criteria

- SQLite and PostgreSQL startups create a usable schema without manual SQL.
- Restarting does not duplicate seed data.
- Default app settings exist after first boot.

### Files likely involved

- `src/Symphony.App/Data/DatabaseInitializer.cs`
- `src/Symphony.App/Data/SymphonyDbContext.cs`
- `src/Symphony.App/Identity/AdminSeeder.cs`
- `src/Symphony.App/Program.cs`

---

## Task T8: Application Dockerfile and development compose

### Priority

High

### Milestone

M2

### Goal

Create the application image using a multi-stage .NET Docker build for GHCR publishing and local compose.

### Scope

- Create `src/Symphony.App/Dockerfile` using `dotnet/sdk:10.0` build and `dotnet/aspnet:10.0` runtime stages.
- Restore and publish the web app for `linux-x64`.
- Create app data directory and run as non-root where practical.
- Expose port `8080` and set `ASPNETCORE_URLS=http://+:8080`.
- Add `.dockerignore` excluding build artifacts, tests, docs, and local data.
- Create root `docker-compose.yml` for source-based local development with PostgreSQL 16, app build context, health checks, volumes, and provider-switching env vars.

### Constraints

- Keep the build context at repo root so restore/publish project paths resolve.
- Do not bake secrets or connection strings into the image.
- Match the CodeMemory image layout as closely as useful.

### Suggested implementation path

- Mirror `E:/khurram-uworx/CodeMemory/src/CodeMemory.AspNet/Dockerfile`.
- Use `dotnet publish ... --no-self-contained` in the build stage.
- Keep Data Protection key persistence on a mounted volume.

### Acceptance criteria

- `docker build -f src/Symphony.App/Dockerfile .` succeeds.
- `docker compose up` (root) starts PostgreSQL and local app.
- App creates schema automatically.
- First-admin credentials print to container logs once.
- Login works against the composed app.

### Files likely involved

- `src/Symphony.App/Dockerfile`
- `.dockerignore`
- `src/Symphony.App/Symphony.App.csproj`
- `src/Symphony.App/Program.cs`

---

## Task T8A: CI workflow (build and test)

### Priority

High

### Milestone

M2

### Goal

Update the existing GitHub Actions CI so it builds and tests only `tests/Symphony.Tests`, collects coverage, and does not treat an empty Release Readiness run as success.

### Scope

- Update `.github/workflows/ci.yml`. Do not add a second CI workflow. The file already exists.
- Trigger on push/pull request to `main`.
- Setup .NET 10 (`10.0.x`).
- Restore, build Release, and test only `tests/Symphony.Tests`. Do not build `tests/Symphony.CodingAgentsProbe` or `tests/Symphony.PerformanceTests`.
- Exclude `Integration`, `Smoke`, and `ReleaseReadiness` from the default CI test run.
- Add `coverlet.collector` to `tests/Symphony.Tests` and collect Cobertura coverage. Fail the job if tests ran and no coverage file was produced.
- Update `.github/workflows/release-readiness.yml` so zero matching tests fail the job. T5 supplies the `ReleaseReadiness` tests; this task owns the workflow.

### Constraints

- Tests should not require external databases in CI.
- Override environment to production to avoid development-only config.

### Docs and workflows

- `.github/workflows/ci.yml` and `.github/workflows/release-readiness.yml` are in scope. Do not edit `cd.yml`; T8B owns release publishing.
- `README.md` Development section must point at `dotnet test tests/Symphony.Tests` and at `ci.yml`, and must say probes and performance tests are not CI.
- `AGENTS.md` Build & Verify must match the workflow filters and coverage expectation.

### Suggested implementation path

- Start from the existing `.github/workflows/ci.yml` and `.github/workflows/release-readiness.yml`.
- Use `dotnet-version: '10.0.x'`.

### Acceptance criteria

- Workflow runs on pull requests and pushes.
- Build/test job passes on a clean checkout.
- Integration, Smoke, and probe/performance projects are not part of the default CI run.
- A CI run that executes tests uploads a Cobertura file.
- Release Readiness fails when the `ReleaseReadiness` filter matches no tests.

### Files likely involved

- `.github/workflows/ci.yml`
- `.github/workflows/release-readiness.yml`
- `tests/Symphony.Tests/Symphony.Tests.csproj`
- `README.md`
- `AGENTS.md`

---

## Task T8B: Docker build and publish workflow (GHCR)

### Priority

High

### Milestone

M2

### Goal

Publish the web app image to GitHub Container Registry so QA can deploy it without rebuilding.

### Scope

- Create `.github/workflows/docker.yml` with manual dispatch input `image_tag` (default `latest`).
- Replace the current `.github/workflows/cd.yml`. It only builds and tests. It must publish the image. Do not add a second publish workflow and leave the non-publishing `cd.yml` in place.
- On `v*` tags and `workflow_dispatch`, log in to `ghcr.io` with `GITHUB_TOKEN` and push `ghcr.io/${{ github.repository_owner }}/symphony` tagged with the version and `latest`.
- Use the application Dockerfile and repo root context.
- Do not pack or push a NuGet package. Symphony is an application.

### Constraints

- Use `packages: write` permission.
- Do not expose registry credentials.
- Tag scheme must be documented.

### Docs and workflows

- `.github/workflows/cd.yml` and `.github/workflows/docker.yml` are in scope.
- `README.md` must say how a release image is published (tag `v*` or manual dispatch) and the image name.
- `AGENTS.md` must point at `cd.yml` as the publish workflow, not a build-only check.

### Suggested implementation path

- Mirror CodeMemory `docker.yml` (manual) and relevant `cd.yml` `docker-push` job.
- Standardize image name as `ghcr.io/${{ github.repository_owner }}/symphony`.

### Acceptance criteria

- A `v*` tag or manual CD run publishes `ghcr.io/${{ github.repository_owner }}/symphony` with a version tag and `latest`.
- Manual `docker.yml` can publish a requested tag.
- Image is pullable according to package visibility.
- Published image starts with PostgreSQL-backed config.
- `README.md` and `AGENTS.md` describe that publish path.

### Files likely involved

- `.github/workflows/docker.yml`
- `.github/workflows/cd.yml`
- `src/Symphony.App/Dockerfile`
- `README.md`
- `AGENTS.md`

---

## Task T8C: Sample compose using published GHCR image and PostgreSQL

### Priority

High

### Milestone

M2

### Goal

Provide a ready-to-run `samples/docker-compose.yml` that spins up the published image with PostgreSQL configured.

### Scope

- Create `samples/docker-compose.yml` (or `samples/docker-compose.postgres.yml`).
- Use `image: ghcr.io/khurram-uworx/symphony:latest` (or owner equivalent) instead of local build.
- Add PostgreSQL 16 service with health check.
- Configure `Database__Provider=postgresql` (or `npgsql`) and `ConnectionStrings__PostgreSQL`/`Npgsql`.
- Mount volumes for PostgreSQL data and Data Protection keys.
- Expose app on a sensible port (for example `8080:8080`).

### Constraints

- Use published image only for sample deployment; keep root `docker-compose.yml` optional for source build.
- Do not embed real secrets; use sample-only default credentials that must be changed.
- Include first-admin credential print behavior.

### Suggested implementation path

- Adapt CodeMemory compose structure but simplify to app + PostgreSQL.
- Align env vars with `Database:Provider` registration.
- Add `depends_on` health condition.

### Acceptance criteria

- `docker compose -f samples/docker-compose.yml up` starts PostgreSQL and published app.
- App schema initializes automatically.
- First-admin credentials print to container logs.
- Login and greeting work.

### Files likely involved

- `samples/docker-compose.yml`
- `src/Symphony.App/Program.cs`
- `CONTAINER-README.md` (or `README.md`)

---

## Task T8D: Container usage documentation and sample environment

### Priority

Medium

### Milestone

M2

### Goal

Document how to run the published image and sample compose, including environment variables and persistence.

### Scope

- Document `ghcr.io/.../symphony` image usage.
- Add environment variable table for provider, connection strings, Data Protection, URLs.
- Document first-admin credential output.
- Document volume persistence.
- Add sample env guidance.

### Docs and workflows

This task finishes the product-story cut. `README.md` still describes the console poller. After T8D, that must not be the documented way to run Symphony.

- `README.md`: replace Getting Started, Running, and How It Works so they describe the web host and the published image. Remove workflow-file polling as the operator path.
- `AGENTS.md`: primary docs and build/run notes must match that story, including the image name and that probes are not CI.
- Do not change workflow logic here. T8A owns `ci.yml` and `release-readiness.yml`. T8B owns `cd.yml` and `docker.yml`.

### Constraints

- Keep secret handling guidance clear.
- Do not duplicate large web plan details.

### Suggested implementation path

- Mirror CodeMemory `CONTAINER-README.md` style.
- Put operational container steps in `CONTAINER-README.md` and make `README.md` the product entry point that links there.

### Acceptance criteria

- DevOps can follow docs to spin up staging.
- Sample compose commands are documented and tested.
- `README.md` and `AGENTS.md` do not document the console poller as the product.

### Files likely involved

- `CONTAINER-README.md`
- `samples/docker-compose.yml`
- `README.md`
- `AGENTS.md`

---

## Task T9: Admin user and role management

### Priority

High

### Milestone

M2

### Goal

Allow admins to create, disable, delete, and assign roles to users through `/admin/users`.

### Why this exists

Users are admin-invited and managed by admins in the first cut.

### Scope

- Implement `/admin/users` Razor Pages or backing API.
- Create member and admin accounts.
- Enable/disable account.
- Delete account.
- Reset credentials as needed.
- Invalidate sessions and API keys on disable/delete.
- Require `Admin` role.

### Constraints

- Admins manage users, not users' personal API keys.
- No public self-registration.

### Suggested implementation path

- Use Identity APIs.
- Protect all endpoints with `[Authorize(Roles = "Admin")]`.
- Provide confirmation UI for destructive actions.

### Acceptance criteria

- Admin can create a member named `alice`.
- Member can log in after account creation.
- Disabled or deleted user cannot authenticate.
- Existing sessions for disabled/deleted users are invalidated.

### Files likely involved

- `src/Symphony.App/Pages/Admin/Users.cshtml`
- `src/Symphony.App/Pages/Admin/Users.cshtml.cs`
- `src/Symphony.App/Services/UserManagementService.cs`

---

## Task T10: Member account area and greeting

### Priority

Medium

### Milestone

M2

### Goal

Add a member-facing account area with a personalized greeting and navigation.

### Scope

- Add `/account` layout and pages.
- Show display name and role.
- Add placeholders for configuration, workflows, API keys, and run launcher.
- Ensure member cannot access `/admin/*`.

### Constraints

- Keep UI simple but navigable.
- Authorization must be enforced server-side.

### Suggested implementation path

- Use shared `_Layout` with role-aware navigation.
- Redirect non-admin users away from admin pages.

### Acceptance criteria

- Both admin and member see a greeting with their username/name.
- Member is forbidden or redirected from `/admin/*`.
- Account area renders without errors.

### Files likely involved

- `src/Symphony.App/Pages/Account/Index.cshtml`
- `src/Symphony.App/Pages/Shared/_Layout.cshtml`
- `src/Symphony.App/Pages/Admin/Index.cshtml`

---

## Task T11: Data Protection persistence and cookie hardening

### Priority

Medium

### Milestone

M2

### Goal

Persist Data Protection keys and harden cookie options for multi-instance and container deployments.

### Scope

- Persist key ring to a shared/volume-backed location.
- Configure `Secure`, `HttpOnly`, `SameSite`, expiry, and sliding renewal.
- Add HTTPS redirection and HSTS in production.

### Constraints

- Keys must survive container restarts.
- Avoid breaking local HTTP development unnecessarily; use conditional production settings.

### Suggested implementation path

- Call `AddDataProtection().PersistKeysToFileSystem(...)`.
- Configure `CookieAuthenticationOptions` via Identity options.
- Configure HSTS conditionally on environment.

### Acceptance criteria

- Data Protection keys are loaded from the configured shared path.
- Auth cookies are `HttpOnly` and `Secure` in production.
- Encryption remains valid after app restart.

### Files likely involved

- `src/Symphony.App/Program.cs`
- `src/Symphony.App/appsettings.json`

---

## Task T12: UserConfiguration persistence and encryption

### Priority

High

### Milestone

M3

### Goal

Persist per-user `UserConfiguration`, including tracker, agent, workspace, hooks, and concurrency settings.

### Why this exists

Each user owns a saved configuration used to build runtime `ServiceConfig`.

### Scope

- Add `UserConfiguration` entity with fields from the web plan.
- Add DbContext mapping and migration.
- Encrypt tracker credentials and Copilot provider secrets at rest.
- Implement 1:1 relationship with user.
- Add created/updated timestamps.

### Constraints

- Secrets must use the secret protection service (T19).
- Do not store plaintext secrets.
- Runtime jobs use a user-scoped folder beneath configured workspace root.

### Suggested implementation path

- Create entity and EF migration.
- Add encryption in repository/service layer.
- Default max turns/concurrency when unset.

### Acceptance criteria

- A configuration row is created/updated per user.
- Secret fields are encrypted in the database.
- Plain secrets are returned only through authorized in-memory operations.
- DB schema includes required columns.

### Files likely involved

- `src/Symphony.App/Domain/UserConfiguration.cs`
- `src/Symphony.App/Data/SymphonyDbContext.cs`
- `src/Symphony.App/Services/ConfigurationService.cs`
- `src/Symphony.App/Data/Migrations/*`

---

## Task T13: Configuration API

### Priority

High

### Milestone

M3

### Goal

Expose `GET /api/configuration` and `PUT /api/configuration` for the current user.

### Scope

- Return current user's configuration.
- Update configuration with validation.
- Mask secret fields in responses.
- Require authenticated user (cookie or API key).
- Enforce ownership.

### Constraints

- API keys do not configure tracker homes; user owns config.
- Copilot BYOM provider type must be one of `openai`, `azure`, `anthropic`.

### Suggested implementation path

- Use minimal APIs or controllers behind `/api`.
- DTOs with secret-write, masked-read semantics.
- Validate base URL, model ID, limits, and states.

### Acceptance criteria

- Authenticated user can fetch and update config.
- Users cannot read or modify another user's config.
- Secrets are never returned in plaintext.
- Invalid provider or limits return `400`.

### Files likely involved

- `src/Symphony.App/Api/ConfigurationEndpoints.cs`
- `src/Symphony.App/Dto/ConfigurationDtos.cs`
- `src/Symphony.App/Services/ConfigurationService.cs`

---

## Task T14: Configuration web page

### Priority

Medium

### Milestone

M3

### Goal

Provide `/account/configuration` for editing personal work tool and agent setup.

### Scope

- Build Razor Page form for tracker settings, states, Copilot BYOM, workspace root, hooks, max turns/concurrency, default workflow.
- Save via API or service.
- Mask existing secrets and support "leave blank to keep".
- Provide provider-specific option sections.

### Constraints

- Do not collect GitHub PAT here.
- Keep form usable without JavaScript where possible.

### Suggested implementation path

- Bind to DTOs rather than entities.
- Show validation errors inline.

### Acceptance criteria

- User saves configuration and sees it after reload.
- Secret inputs are not pre-filled with plaintext.
- Form errors are displayed.
- Provider-specific fields update based on selection.

### Files likely involved

- `src/Symphony.App/Pages/Account/Configuration.cshtml`
- `src/Symphony.App/Pages/Account/Configuration.cshtml.cs`
- `src/Symphony.App/Dto/ConfigurationDtos.cs`

---

## Task T15: Workflow persistence

### Priority

High

### Milestone

M3

### Goal

Persist named, user-owned workflows with unique names per owner.

### Scope

- Add `Workflow` entity with `Id`, `OwnerUserId`, `Name`, `Content`, `IsDefault`, timestamps.
- Add unique index on `(OwnerUserId, Name)`.
- Add migration.
- Enforce at least one workflow per owner (service-level guard).

### Constraints

- A job stores workflow name, not content.
- Workflow content is resolved by name at run time.

### Suggested implementation path

- Create entity and DbContext mapping.
- Add repository/service methods.

### Acceptance criteria

- Multiple workflows can be stored per user.
- Duplicate names for the same owner are rejected.
- Each owner must retain at least one workflow.
- Index exists in schema.

### Files likely involved

- `src/Symphony.App/Domain/Workflow.cs`
- `src/Symphony.App/Data/SymphonyDbContext.cs`
- `src/Symphony.App/Services/WorkflowService.cs`

---

## Task T16: Workflow CRUD API

### Priority

High

### Milestone

M3

### Goal

Expose user-owned workflow endpoints under `/api/workflows`.

### Scope

- `GET /api/workflows` list current user's workflows.
- `GET /api/workflows/{name}` get one workflow.
- `POST /api/workflows` create named workflow.
- `PUT /api/workflows/{name}` update workflow.
- `DELETE /api/workflows/{name}` delete workflow (not last).
- Enforce ownership and validation.

### Constraints

- Only owners manage their workflows in first cut.
- Admins do not need to edit user workflows for jobs because job uses owner's workflow.
- Cannot delete the last remaining workflow.

### Suggested implementation path

- Case-sensitive or invariant name handling should be documented and consistent.
- Validate content is non-empty.

### Acceptance criteria

- CRUD works for authenticated owner.
- Users cannot access another user's workflows.
- Delete last workflow returns `409` or `400`.
- Renamed workflow references are either handled or explicitly rejected (document behavior).

### Files likely involved

- `src/Symphony.App/Api/WorkflowEndpoints.cs`
- `src/Symphony.App/Dto/WorkflowDtos.cs`
- `src/Symphony.App/Services/WorkflowService.cs`

---

## Task T17: Workflow web editor

### Priority

Medium

### Milestone

M3

### Goal

Provide `/account/workflows` to create, edit, rename, copy, and delete workflows.

### Scope

- List workflows.
- Editor for full WORKFLOW.md-style content.
- Create from blank or copy from `WORKFLOW`.
- Delete with confirmation.
- Show default flag.

### Constraints

- At least one workflow remains.
- Editor should preserve markdown/front matter.

### Suggested implementation path

- Use a textarea first; rich editor optional later.
- Use API endpoints from T16.

### Acceptance criteria

- User creates/edits/deletes workflows from the page.
- Bundled `WORKFLOW` is editable.
- Last workflow cannot be deleted from UI.
- Content round-trips unchanged.

### Files likely involved

- `src/Symphony.App/Pages/Account/Workflows.cshtml`
- `src/Symphony.App/Pages/Account/Workflows.cshtml.cs`
- `src/Symphony.App/Api/WorkflowEndpoints.cs`

---

## Task T18: Default workflow and app settings seeding

### Priority

High

### Milestone

M3

### Goal

Seed the bundled default `WORKFLOW` workflow and default app settings for users/server.

### Scope

- Import existing `WORKFLOW.md` content into app resources.
- Seed every user with no workflows with a workflow named `WORKFLOW`.
- Seed default `AppSetting` values (global concurrency, default workspace root, max retry backoff).
- Seed new users on account creation too.

### Constraints

- Seeding is idempotent.
- Bundled content must remain available in published output.

### Suggested implementation path

- Embed or copy `WORKFLOW.md` as content.
- Add seeding routine to database initializer and user creation path.
- Seed only when user has no workflows.

### Acceptance criteria

- New and existing users with no workflows get `WORKFLOW`.
- Users with workflows are not overwritten.
- Required default app settings exist after first boot.
- Bundled content matches original `WORKFLOW.md`.

### Files likely involved

- `src/Symphony.App/WORKFLOW.md`
- `src/Symphony.App/Data/DatabaseInitializer.cs`
- `src/Symphony.App/Services/UserManagementService.cs`
- `src/Symphony.App/Services/WorkflowService.cs`

---

## Task T19: Secret protection service

### Priority

High

### Milestone

M3 (decision gate)

### Goal

Provide field-level encryption/decryption using ASP.NET Core Data Protection for secrets.

### Why this exists

Tracker credentials, Copilot BYOM secrets, encrypted PAT snapshots, and API key hashes need protected storage.

### Scope

- Implement `ISecretProtector`/`SecretProtector`.
- Wrap Data Protection with purpose strings.
- Add redaction helpers to ensure secrets are not logged or serialized accidentally.
- Unit tests for roundtrip and non-equality of ciphertext to plaintext.

### Constraints

- Use Data Protection now; vault integration is a non-goal.
- PAT snapshots, if stored, may only contain encrypted PAT.
- Redaction must cover logs, job events, and status responses.

### Suggested implementation path

- Register protector with a dedicated protector purpose.
- Add `RedactSecrets` utilities for strings/objects.
- Provide test-friendly key ring location.

### Acceptance criteria

- Roundtrip encryption succeeds.
- Null/empty input handled.
- Ciphertext differs from plaintext.
- Helpers remove secret-like fields from event payloads.

### Files likely involved

- `src/Symphony.App/Security/SecretProtector.cs`
- `src/Symphony.App/Security/Redaction.cs`
- `tests/Symphony.Tests/SecretProtectorTests.cs`

---

## Task T20: ServiceConfigFactory

### Priority

High

### Milestone

M3 (decision gate)

### Goal

Create `ServiceConfigFactory.Build(...)` from user configuration, workflow content, and request data.

### Scope

- Build `TrackerConfig` from user config.
- Build `WorkspaceConfig`, `HooksConfig`, `AgentConfig`.
- Build Copilot BYOM `CopilotConfig`.
- Parse selected workflow content into `WorkflowDefinition` config map and prompt template.
- Apply workflow front matter overrides (workspace, hooks, turn limits).
- Support `NullIssueTracker` semantics for inline work through request/factory selection.
- Auto-include submitted item states in active states for posted work.

### Constraints

- Request-scoped GitHub PAT/repository are not part of saved config.
- Codex-related front matter is ignored.
- Copilot provider type restricted to supported values.

### Suggested implementation path

- Add factory static/build method.
- Unit-test every supported path.
- Add inline work state defaults.

### Decision required

Define allowed front matter override keys and validation bounds.

### Acceptance criteria

- Unit tests cover Jira/Linear config build.
- Inline work config produces runnable active states.
- Copilot BYOM fields are populated correctly.
- Front matter overrides config without replacing tracker.
- Codex front matter ignored.

### Files likely involved

- `src/Symphony.App/Config/ServiceConfigFactory.cs`
- `src/Symphony.App/Config/ServiceConfigProvider.cs`
- `src/Symphony.App/Config/CopilotConfig.cs`
- `src/Symphony.App/Workflows/WorkflowLoader.cs`
- `tests/Symphony.Tests/ServiceConfigFactoryTests.cs`

---

## Task T21: Job aggregate persistence

### Priority

High

### Milestone

M3

### Goal

Persist `Job`, `JobIssue`, and `JobEvent` aggregates with required indices.

### Scope

- Add entities and DbContext mappings.
- Add unique/lookup indices:
  - `Job(State, QueuedAt)`
  - `Job(OwnerUserId, QueuedAt)`
  - `JobIssue(JobId)`
  - `JobEvent(JobId, Timestamp)`
- Add migration.
- Include fields for workflow name, retry link, repository, encrypted snapshot, state timings, summary/error fields.

### Constraints

- Do not store plaintext PAT.
- Store workflow name only; resolve content by name at run time.

### Suggested implementation path

- Add entities in `Domain`.
- Map relationships and cascade delete for issues/events.
- Use Guid IDs.

### Acceptance criteria

- Job aggregate tables exist with required fields.
- Indices exist in schema.
- Persisting a job with issues/events succeeds.
- Build remains green.

### Files likely involved

- `src/Symphony.App/Domain/Job.cs`
- `src/Symphony.App/Domain/JobIssue.cs`
- `src/Symphony.App/Domain/JobEvent.cs`
- `src/Symphony.App/Data/SymphonyDbContext.cs`
- `src/Symphony.App/Data/Migrations/*`

---

## Task T22: Job submission API

### Priority

High

### Milestone

M3

### Goal

Implement `POST /api/work` to queue explicitly submitted work.

### Scope

- Accept `externalId`, `ticketIds`, `workItems`, `github.repository`, `github.personalAccessToken`, `workflowName`, `overrides`.
- Validate at least one ticket/item, required repository/PAT/workflow.
- Validate workflow exists and is owned by requester.
- Create `Job` with `State = Queued`.
- Store encrypted request snapshot.
- Return `202 { jobId, statusUrl }`.
- Wake job processor.

### Constraints

- Never return PAT or encrypted snapshot.
- PAT is never stored in plaintext.
- Workflow name is required; default config field is UI convenience only.

### Suggested implementation path

- Use minimal APIs with authentication.
- Resolve owner from cookie or API key.
- Persist snapshot through `ISecretProtector`.

### Acceptance criteria

- Valid mixed submission creates queued job.
- Missing ticket/item, repository, PAT, or workflow gives `400`.
- Invalid/unowned workflow gives `400`/`404`.
- Response contains `jobId` and `statusUrl` only, no secrets.

### Files likely involved

- `src/Symphony.App/Api/WorkEndpoints.cs`
- `src/Symphony.App/Dto/WorkRequestDtos.cs`
- `src/Symphony.App/Services/JobService.cs`
- `src/Symphony.App/Security/SecretProtector.cs`

---

## Task T23: Run launcher page

### Priority

Medium

### Milestone

M3

### Goal

Provide `/account/run` as a friendly launcher over the same submission payload.

### Scope

- Tabbed UI:
  - Run: form for ticket IDs/work items, repository, PAT, workflow selection, optional overrides, submit to same API.
  - API: copy-pasteable curl example; guidance when no API key exists.
- Show validation and job result link.

### Constraints

- Must submit through same API path as `POST /api/work`.
- API tab PAT placeholder must not expose a real secret.

### Suggested implementation path

- Reuse workflow list endpoint for selection.
- Use fetch/XHR to call `/api/work`.
- Render default `WORKFLOW` preselected from user config if present.

### Acceptance criteria

- User can submit work from the form.
- Submitted job appears on job pages.
- API tab shows valid curl skeleton.
- Form uses workflow selection and repository/PAT inputs.

### Files likely involved

- `src/Symphony.App/Pages/Account/Run.cshtml`
- `src/Symphony.App/Pages/Account/Run.cshtml.cs`
- `src/Symphony.App/Api/WorkEndpoints.cs`

---

## Task T24: Database-backed queue wake-up signal

### Priority

Medium

### Milestone

M3

### Goal

Use the database as source of truth, with an in-memory signal to wake job processing promptly.

### Scope

- Implement `JobQueue` using channel or semaphore as notification only.
- `NotifyNewJob` called after enqueue.
- Processor still polls DB on intervals.
- Avoid in-memory job state as source of truth.

### Constraints

- In-memory channel is not authoritative.
- Must not lose queued work if app restarts.

### Suggested implementation path

- Simple `Channel<bool>` or `SemaphoreSlim`.
- `await jobQueue.SignalAsync()`.

### Acceptance criteria

- Enqueuing signals processor without direct dependency.
- Restarted app processes pre-existing queued jobs.
- Signal failure does not fail job creation.

### Files likely involved

- `src/Symphony.App/Processing/JobQueue.cs`
- `src/Symphony.App/Processing/JobProcessor.cs`
- `src/Symphony.App/Api/WorkEndpoints.cs`

---

## Task T25: IServiceConfigProvider refactor

### Priority

High

### Milestone

M4 (decision gate)

### Goal

Make runtime config injectable and mockable instead of using a concrete singleton.

### Scope

- Introduce `IServiceConfigProvider` with `GetConfig()`.
- Make `ServiceConfigProvider` implement it.
- Add `StaticServiceConfigProvider` for per-job runtime config.
- Update `AgentRunner` and `WorkspaceManager` to depend on the interface.

### Constraints

- Keep public constructor changes compatible with DI.
- Do not change run algorithm behavior.

### Suggested implementation path

- Interface in `Config`.
- Update AgentRunner/WorkspaceManager fields and constructor.
- Update DI registration.

### Acceptance criteria

- Code compiles with interface usage.
- Existing AgentRunner behavior unchanged.
- Workspace traversal safety remains.
- Unit tests can inject static configs.

### Files likely involved

- `src/Symphony.App/Config/IServiceConfigProvider.cs`
- `src/Symphony.App/Config/ServiceConfigProvider.cs`
- `src/Symphony.App/Config/StaticServiceConfigProvider.cs`
- `src/Symphony.App/Agent/AgentRunner.cs`
- `src/Symphony.App/Workspaces/WorkspaceManager.cs`

---

## Task T26: Issue tracker factory and NullIssueTracker

### Priority

High

### Milestone

M4

### Goal

Select the correct issue tracker from config and support inline work with `NullIssueTracker`.

### Scope

- Implement `IssueTrackerFactory.Create(ServiceConfig, submissionContext)`.
- Linear/Jira clients as configured.
- `NullIssueTracker` returns inputs unchanged for state fetch, empty candidates, and supports refresh.
- Use tracker for ticket context fetch without polling.

### Constraints

- Do not use candidate polling in the web product.
- Inline work must still pass state refresh semantics.

### Suggested implementation path

- Factory returns `IIssueTracker`.
- Null tracker no-op implementations.
- Auto-include submitted states in service config.

### Acceptance criteria

- Linear and Jira trackers can be created from config.
- Inline-only work uses NullIssueTracker.
- State refresh works without external updates for inline.
- Candidate fetch not used by processor.

### Files likely involved

- `src/Symphony.App/IssueTracker/IssueTrackerFactory.cs`
- `src/Symphony.App/IssueTracker/NullIssueTracker.cs`
- `tests/Symphony.Tests/NullIssueTrackerTests.cs`

---

## Task T27: Coding agent factory (Copilot only)

### Priority

High

### Milestone

M4

### Goal

Create Copilot SDK agents from service config with BYOM settings; retain Codex source but do not register it.

### Scope

- Implement `CodingAgentFactory.Create(ServiceConfig)`.
- Copilot SDK BYOK provider type, base URL, model ID, secret, optional OpenAI wire API.
- Do not register `CodexAgent`.
- Ignore Codex-related front matter during workflow parsing.

### Constraints

- Codex runtime support is not a first-cut goal.
- Copilot SDK BYOM model ID must be used for provider and session configuration.

### Suggested implementation path

- Factory returns `ICodingAgent`.
- Copilot agent constructor updated if needed to accept config.
- Exclude Codex options from service config response/UI.

### Acceptance criteria

- Factory returns Copilot agent for all supported BYOM provider types.
- Codex is not registered in DI.
- Copilot BYOM settings flow into agent initialization.
- Config validation rejects unsupported provider type.

### Files likely involved

- `src/Symphony.App/Agent/CodingAgentFactory.cs`
- `src/Symphony.App/Agent/CopilotAgent.cs`
- `src/Symphony.App/Program.cs`
- `src/Symphony.App/Config/ServiceConfig.cs`

---

## Task T28: Workflow front matter overrides

### Priority

Medium

### Milestone

M4

### Goal

Allow workflow front matter to override run-level workspace, hooks, and agent limits while keeping tracker from user config.

### Scope

- Parse front matter safely.
- ServiceConfigFactory applies allowed overrides.
- Ignore Codex section/front matter.
- Document allowed override keys.

### Constraints

- Tracker kind/credentials remain from user config.
- Invalid front matter should fail validation with a clear error.

### Suggested implementation path

- Reuse WorkflowLoader parsing.
- Merge config maps with precedence: workflow overrides then user config.

### Acceptance criteria

- Workspace/hooks/turns can be changed by workflow front matter.
- Tracker config is not replaced by front matter.
- Malformed front matter returns clear error.
- Codex keys ignored.

### Files likely involved

- `src/Symphony.App/Workflows/WorkflowLoader.cs`
- `src/Symphony.App/Config/ServiceConfigFactory.cs`
- `src/Symphony.App/Config/ConfigReader` (in `ServiceConfigProvider.cs`)

---

## Task T29: Background job processor

### Priority

High

### Milestone

M4

### Goal

Process queued jobs from the database with concurrency controls and per-issue execution.

### Scope

- Implement `JobProcessor` hosted service.
- On startup:
  - recover queued jobs
  - mark stale running jobs failed and emit interrupted-run event
- Poll database respecting global concurrency.
- For each job:
  - mark Running
  - resolve owner config and workflow
  - resolve issues
  - sort by priority then created date
  - run per issue with retry
  - persist issue status and events
  - mark final state
- Per-user workspace folder under configured base root.
- CTS linked to app stopping token.

### Constraints

- DB remains source of truth.
- In-memory channel is wake-up only.
- Per-user limits are primary; global is safety ceiling.
- Per-tracker-state limits apply when dispatching issues.

### Suggested implementation path

- Use service scope per job.
- Persist events incrementally.
- Reuse `AgentRunner.RunAttemptAsync`.
- Retry with exponential backoff up to max attempts.

### Acceptance criteria

- Queued inline job reaches `Completed`.
- Failed job reaches `Failed` with error.
- Cancelled job reaches `Cancelled`.
- Restart reprocesses queued and recovers stale running.
- Workspace path is user-scoped.

### Files likely involved

- `src/Symphony.App/Processing/JobProcessor.cs`
- `src/Symphony.App/Services/JobService.cs`
- `src/Symphony.App/Config/ServiceConfigFactory.cs`
- `src/Symphony.App/Workspaces/WorkspaceManager.cs`

---

## Task T30: Job issue and event persistence

### Priority

High

### Milestone

M4

### Goal

Persist per-issue outcomes and telemetry events during job processing.

### Scope

- Create/update `JobIssue` rows with identifier, title, state, success, reason, turns, workspace path, timings.
- Append `JobEvent` rows with timestamp, event name, summary, payload JSON.
- Redact secret-like fields from payloads.
- Mark job final summary JSON.

### Constraints

- No plaintext PAT in payloads.
- Event append must not fail job due to optional logging, but errors should be recorded.

### Suggested implementation path

- Add methods to `JobService`.
- Use DTO snapshots.
- Wrap event append in try/catch with warning logs.

### Acceptance criteria

- Job detail shows issue outcomes.
- Events appear in chronological order.
- Event payloads contain no PAT or API key.
- Failed event append is logged but does not crash the run.

### Files likely involved

- `src/Symphony.App/Services/JobService.cs`
- `src/Symphony.App/Processing/JobProcessor.cs`
- `src/Symphony.App/Security/Redaction.cs`

---

## Task T31: Tracker-based ticket resolution

### Priority

High

### Milestone

M4

### Goal

Fetch issue details from the owner's configured tracker when `ticketIds` are supplied.

### Scope

- For each requested ticket ID, call `FetchIssueStatesByIdsAsync` or equivalent.
- Map fetched data to `Domain.Issue`.
- Combine with inline work items.
- Auto-include their states in active states.
- Handle missing tickets with job/issue error recording.

### Constraints

- This is context fetching, not polling.
- Partial failures should be reflected in job result.

### Suggested implementation path

- Resolve in job processor after loading job.
- Store resolved issue IDs on `JobIssue`.
- Continue when some ticket fetches fail.

### Acceptance criteria

- Linear ticket IDs resolve to issue details and run.
- Jira ticket IDs resolve to issue details and run.
- Inline items still work when mixed.
- Missing ticket results in a failed issue row, not silent skip.

### Files likely involved

- `src/Symphony.App/Processing/JobProcessor.cs`
- `src/Symphony.App/IssueTracker/LinearClient.cs`
- `src/Symphony.App/IssueTracker/JiraClient.cs`
- `src/Symphony.App/Domain/Models.cs`

---

## Task T32: Job query and cancellation API

### Priority

High

### Milestone

M4

### Goal

Expose job status, list, and cancellation endpoints with ownership/admin rules.

### Scope

- `GET /api/jobs`: owner-only or admin filters.
- `GET /api/jobs/{id}`: owner or admin.
- `DELETE /api/jobs/{id}`: cancel owned job or any job for admin.
- Return masked status DTO with issues and errors.
- Never return secrets.

### Constraints

- Admin sees all jobs; users see only their own.
- Cancel sets cancellation token and processor marks `Cancelled`.

### Suggested implementation path

- Minimal APIs with auth and ownership checks.
- Map `Job` to status DTO.

### Acceptance criteria

- User cannot see other users' jobs.
- Admin can list/filter all jobs.
- Cancel stops processing and job state becomes `Cancelled`.
- Status DTO has no secret or snapshot fields.

### Files likely involved

- `src/Symphony.App/Api/JobEndpoints.cs`
- `src/Symphony.App/Dto/JobStatusDtos.cs`
- `src/Symphony.App/Services/JobService.cs`

---

## Task T33: Admin job pages

### Priority

Medium

### Milestone

M4

### Goal

Provide `/admin/jobs` and `/admin/jobs/{id}` with filtering, pagination, issue timeline, and event tail.

### Scope

- Filter by state/owner/date.
- Paginate results.
- Job detail shows issues and event tail.
- Show retry/cancel controls where authorized.

### Constraints

- Admin only.
- Polling-based updates are acceptable in phase 1.

### Suggested implementation path

- Razor Pages with query binding.
- Client-side refresh every 3-5 seconds.

### Acceptance criteria

- Admin can filter jobs by owner/status.
- Job detail displays issues and recent events.
- Pages handle empty state gracefully.
- Pagination works.

### Files likely involved

- `src/Symphony.App/Pages/Admin/Jobs.cshtml`
- `src/Symphony.App/Pages/Admin/Jobs.cshtml.cs`
- `src/Symphony.App/Pages/Admin/JobDetail.cshtml`
- `src/Symphony.App/Pages/Admin/JobDetail.cshtml.cs`

---

## Task T34: Jira end-to-end smoke validation

### Priority

Medium

### Milestone

M4

### Goal

Validate that Symphony can complete a job for a real Jira ticket end to end.

### Scope

- Document manual setup: Jira credentials, project, workflow, repo/PAT.
- Add an environment-gated integration test or smoke script if feasible.
- Verify job reaches `Completed` or `CompletedWithFailures` with useful issue state.

### Constraints

- No hardcoded credentials.
- Test should be skipped when env vars are missing.

### Suggested implementation path

- Add `NUnitConstants` category `Smoke`.
- Use WebApplicationFactory and configured provider.

### Acceptance criteria

- Given valid Jira credentials, a real ticket job can run to completion.
- Job events and issue row are persisted.
- At least one successful validation is recorded in release notes.

### Files likely involved

- `tests/Symphony.Tests/JiraEndToEndSmokeTests.cs`
- `docs/plans/SYMPHONY-WEB.md`

---

## Task T35: GitHub repository and PAT handling

### Priority

High

### Milestone

M5 (decision gate)

### Goal

Safely accept, store, and use per-request GitHub repository and PAT.

### Scope

- Validate repository format (owner/name or approved URL form).
- Encrypt PAT in request snapshot.
- Pass PAT/repository to job processor at runtime.
- Ensure PAT never appears in DB plaintext, events, logs, status responses.
- Add redaction helpers and tests.

### Constraints

- PAT is request-scoped and never saved in configuration.
- Job retry stores/uses encrypted PAT from original snapshot.
- If snapshot lacks PAT, retry must fail gracefully.

### Suggested implementation path

- Add repository validation utility.
- Snapshot PAT only encrypted.
- Redact in logger/event payload.

### Acceptance criteria

- Missing repository/PAT fails validation.
- PAT is encrypted at rest.
- Status/event/log traces do not contain PAT.
- Retry can use encrypted PAT from snapshot.

### Files likely involved

- `src/Symphony.App/Security/GitHubPatRedaction.cs`
- `src/Symphony.App/Dto/WorkRequestDtos.cs`
- `src/Symphony.App/Services/JobService.cs`

---

## Task T36: Copilot BYOM end-to-end

### Priority

Medium

### Milestone

M5

### Goal

Verify Copilot SDK agents run using each user's BYOM settings.

### Scope

- Configure provider type, base URL, model ID, secret, optional OpenAI wire API.
- Initialize Copilot session with BYOK.
- Run a job through a local/minimal repository change task.
- Record events for agent initialization.

### Constraints

- Secret is user-scoped and encrypted.
- Model ID is used for both provider and session configuration.

### Suggested implementation path

- Add integration smoke test with env-gated Copilot credentials.
- Keep test skip-friendly.

### Acceptance criteria

- A job using Copilot BYOM reaches at least agent initialization.
- Missing BYOM fields produce validation errors.
- Agent events record provider/model metadata without secrets.

### Files likely involved

- `src/Symphony.App/Agent/CopilotAgent.cs`
- `src/Symphony.App/Config/ServiceConfigFactory.cs`
- `tests/Symphony.Tests/CopilotByomSmokeTests.cs`

---

## Task T37: User-scoped workspaces and concurrency enforcement

### Priority

Medium

### Milestone

M5

### Goal

Ensure jobs run in user-scoped workspace folders and respect per-user/global/per-state limits.

### Scope

- Enforce `/data/{userId}` (or equivalent per base root) as runtime root.
- Prevent cross-user workspace collisions.
- Apply per-user max concurrency as primary scheduling constraint.
- Apply global safety ceiling and per-state limits.
- Validate workspace base roots against traversal/relative paths.

### Constraints

- Workspaces are never shared between users.
- Host/container path mapping may vary.
- Open decision: workspace root safety validation.

### Suggested implementation path

- Update `WorkspaceManager` to accept scoped root from config.
- Add path guard tests.
- Track running jobs by owner for concurrency.

### Acceptance criteria

- Two users' workspaces cannot collide.
- Per-user limit prevents over-dispatch.
- Global ceiling enforced.
- Traversal attempts are rejected.

### Files likely involved

- `src/Symphony.App/Workspaces/WorkspaceManager.cs`
- `src/Symphony.App/Processing/JobProcessor.cs`
- `tests/Symphony.Tests/WorkspacePathSafetyTests.cs`

---

## Task T38: Symphony repository first end-to-end run

### Priority

Medium

### Milestone

M5

### Goal

Run a job against Symphony's own repository and complete a small issue end to end.

### Scope

- Manual/scripted run with a real GitHub PAT and workflow.
- Verify workspace, hooks, agent turn loop, ticket state refresh, workspace cleanup.
- Document setup and command.

### Constraints

- No plaintext PAT in task output except masked.
- Use a sandbox branch for changes.

### Suggested implementation path

- Reuse T34 smoke flow.
- Add a demo workflow copy for repository edits.

### Acceptance criteria

- Symphony starts a job against its own repo.
- Agent completes configured turns.
- Job reaches terminal state.
- Workspace before/after hooks run.

### Files likely involved

- `tests/Symphony.Tests/` (smoke scripts)
- `docs/plans/SYMPHONY-WEB.md`

---

## Task T39: API key management

### Priority

High

### Milestone

M6

### Goal

Add user-owned API keys with create, rotate, enable/disable, revoke, and metadata listing.

### Scope

- Add `ApiKey` entity and migration.
- Store hashed keys only; show raw key once.
- Prefix lookup and constant-time hash verification.
- Enforce enabled, expiry, revocation.
- Add endpoints:
  - `GET /api/api-keys`
  - `POST /api/api-keys`
  - `POST /api/api-keys/{id}/rotate`
  - `PATCH /api/api-keys/{id}`
  - `DELETE /api/api-keys/{id}`
- Add `/account/api-keys` page.
- Invalidate keys on user disable/delete.

### Constraints

- Admins do not manage other users' personal keys.
- Hashing and constant-time compare.
- Raw key shown only once.

### Suggested implementation path

- Use secure random generation.
- Store prefix + hash.
- Middleware resolves owner and attributes request.

### Acceptance criteria

- User can create and use an API key.
- Disabled/revoked/expired key fails.
- Rotate issues new key and invalidates old.
- Metadata list hides raw key.
- Ownership is enforced.

### Files likely involved

- `src/Symphony.App/Domain/ApiKey.cs`
- `src/Symphony.App/Data/SymphonyDbContext.cs`
- `src/Symphony.App/Api/ApiKeyEndpoints.cs`
- `src/Symphony.App/Middleware/ApiKeyMiddleware.cs`
- `src/Symphony.App/Pages/Account/ApiKeys.cshtml`

---

## Task T40: API key tests

### Priority

Medium

### Milestone

M6

### Goal

Validate API key middleware and lifecycle behavior.

### Scope

- Enable/disable behavior.
- Rotation revoke behavior.
- Expiry checks.
- Constant-time-ish comparison path and hash verification.
- Ownership and admin non-access to personal keys.

### Constraints

- No raw key assertions after creation.
- Keep tests deterministic.

### Suggested implementation path

- Unit tests for service plus integration tests for endpoints.
- Use temp SQLite.

### Acceptance criteria

- All lifecycle tests pass.
- Disabled key returns `401`.
- Rotated old key returns `401`.
- Valid key attributes request to owner.

### Files likely involved

- `tests/Symphony.Tests/ApiKeyServiceTests.cs`
- `tests/Symphony.Tests/ApiKeyMiddlewareTests.cs`

---

## Task T41: curl-oriented run page documentation

### Priority

Medium

### Milestone

M6

### Goal

Make `/account/run` API tab show a live curl example using the user's actual enabled API key.

### Scope

- Display current enabled API key metadata or prompt to create one.
- Generate curl command for the selected/default workflow.
- Copy-to-clipboard helper optional.
- Explain request fields and secret handling.

### Constraints

- Never embed a real secret in static HTML.
- Raw key only inserted on creation/rotation.

### Suggested implementation path

- Render selected key prefix and use placeholder token input.
- Server-render command template with token slot.

### Acceptance criteria

- User with enabled key sees usable curl template.
- User without key sees creation guidance.
- Curl matches actual endpoint contract.

### Files likely involved

- `src/Symphony.App/Pages/Account/Run.cshtml`
- `src/Symphony.App/Pages/Account/ApiKeys.cshtml`

---

## Task T42: API validation and rate limiting

### Priority

Medium

### Milestone

M6

### Goal

Harden public API endpoints with validation and rate limits.

### Scope

- Add request validation for all API DTOs.
- Add rate limiting policies for API endpoints.
- Consistent error responses.
- Reject malformed work items/repositories early.

### Constraints

- Do not block normal member usage.
- Keep integration tests stable by using test configuration overrides.

### Suggested implementation path

- Use ASP.NET Core rate limiter middleware.
- Add FluentValidation or manual validation.

### Acceptance criteria

- Invalid requests return consistent `400`.
- Excessive requests return `429`.
- Core smoke tests still pass under default limits.

### Files likely involved

- `src/Symphony.App/Program.cs`
- `src/Symphony.App/Api/*`
- `src/Symphony.App/Dto/*`

---

## Task T43: Arbitrary repository and ticket curl verification

### Priority

Medium

### Milestone

M6

### Goal

Verify a terminal curl submission succeeds for an arbitrary configured repository and ticket.

### Scope

- Document full curl example.
- Test with API key against local app.
- Use a sandbox repo and ticket or inline work.
- Verify `202`, status URL, and eventual terminal job state.

### Constraints

- No real secrets in committed docs beyond placeholders.
- Tests/manual steps must be reproducible by QA.

### Suggested implementation path

- Add QA smoke script/commands.
- Gate real network tests behind env vars.

### Acceptance criteria

- A terminal curl call queues a job.
- Job can be polled to terminal state.
- Errors are visible and actionable.

### Files likely involved

- `tests/Symphony.Tests/`
- `docs/plans/SYMPHONY-WEB-PLAN.md` (this file)
- `README.md`

---

## Task T44: Admin retry linked jobs

### Priority

High

### Milestone

M7 (decision gate)

### Goal

Allow admins to retry a job by creating a new linked job.

### Scope

- `POST /api/jobs/{id}/retry` (admin only).
- Create new job with `RetryOfJobId` set.
- Reuse original request/snapshot plus current workflow resolution.
- Mark linkage in status DTO.
- Decide and document workflow name resolution for retries.

### Constraints

- Do not mutate original job.
- PAT must come from encrypted snapshot; if missing, fail gracefully.
- Admin only.

### Suggested implementation path

- Clone job metadata.
- Enqueue new job.
- Return new `jobId`.

### Acceptance criteria

- Retry creates new job linked to original.
- Original job remains unchanged.
- New job has valid request/snapshot.
- Cross-user retry forbidden for non-admins.

### Files likely involved

- `src/Symphony.App/Api/JobEndpoints.cs`
- `src/Symphony.App/Services/JobService.cs`
- `src/Symphony.App/Dto/JobStatusDtos.cs`

---

## Task T45: Cancellation, stale recovery, and cleanup

### Priority

High

### Milestone

M7

### Goal

Ensure job cancellation, stale running recovery, and workspace cleanup work reliably.

### Scope

- `DELETE /api/jobs/{id}` cancels job CTS.
- Processor catches cancellation and marks `Cancelled`.
- On startup, stale running jobs become `Failed` with interrupted-run event.
- Workspace cleanup on terminal/cancellation.
- CTS linked to app stopping token.

### Constraints

- Avoid orphaned cancellation sources.
- Cleanup best-effort; log failures.

### Suggested implementation path

- Track CTS per job in processor state.
- Add recovery routine in hosted service startup.
- Use existing cleanup code.

### Acceptance criteria

- Cancel stops active issue and job becomes `Cancelled`.
- Restart marks stale running jobs `Failed`.
- Interrupted-run event is emitted.
- Workspace directories are removed where possible.

### Files likely involved

- `src/Symphony.App/Processing/JobProcessor.cs`
- `src/Symphony.App/Workspaces/WorkspaceManager.cs`
- `tests/Symphony.Tests/JobRecoveryTests.cs`

---

## Task T46: Real-time job updates with SignalR

### Priority

Medium

### Milestone

M7

### Goal

Add SignalR push for job progress while keeping polling fallback.

### Scope

- Add SignalR hub for job updates.
- Processor/service pushes updates on state changes and events.
- UI subscribes to owned job updates.
- Keep polling fallback if websocket unavailable.

### Constraints

- Do not make UI dependent solely on SignalR for correctness.
- Security: users receive only own job updates (admin all).

### Suggested implementation path

- `JobUpdateHub`.
- Group connections by user/admin.
- Client reconnect fallback.

### Acceptance criteria

- Job pages update without manual refresh when SignalR works.
- Polling fallback still shows correct state.
- Unauthorized users cannot receive others' updates.

### Files likely involved

- `src/Symphony.App/Hubs/JobUpdateHub.cs`
- `src/Symphony.App/Pages/Admin/Jobs.cshtml`
- `src/Symphony.App/Pages/Account/...cshtml`
- `src/Symphony.App/Program.cs`

---

## Task T47: Admin settings and app settings API

### Priority

Medium

### Milestone

M7

### Goal

Allow admins to view/update global settings such as concurrency, default workspace root, and retry backoff.

### Scope

- `/admin/settings` page.
- Backing API for `AppSetting` get/update.
- Apply settings to processor scheduling.
- Validate values.

### Constraints

- Only admins can modify global settings.
- Invalid values should not break processing.

### Suggested implementation path

- Store typed settings with defaults fallback.
- Processor reloads on tick.

### Acceptance criteria

- Admin can update global concurrency and retry backoff.
- Members cannot access admin settings.
- Processor uses updated settings without restart.

### Files likely involved

- `src/Symphony.App/Pages/Admin/Settings.cshtml`
- `src/Symphony.App/Api/AppSettingEndpoints.cs`
- `src/Symphony.App/Services/AppSettingsService.cs`

---

## Task T48: Redaction and encrypted snapshot access

### Priority

Medium

### Milestone

M7

### Goal

Ensure audit snapshots are protected and not surfaced through normal APIs/UI.

### Scope

- Keep encrypted request snapshot encrypted at rest.
- Never return snapshot or PAT in job status.
- Admin audit metadata can reference snapshot existence but not content unless explicitly authorized future feature.

### Constraints

- Snapshot decryption must not happen on read endpoints.
- Events/logs redacted.

### Suggested implementation path

- Separate entity fields and DTO projection.
- Add tests for status responses.

### Acceptance criteria

- Job status response excludes snapshot.
- Event payloads exclude secret fields.
- Logs omit PAT.

### Files likely involved

- `src/Symphony.App/Dto/JobStatusDtos.cs`
- `src/Symphony.App/Services/JobService.cs`
- `src/Symphony.App/Security/Redaction.cs`

---

## Task T49: Provider matrix tests

### Priority

Medium

### Milestone

M7

### Goal

Test schema initialization/migrations and basic flows across supported providers when available.

### Scope

- Integration tests for SQLite always.
- SQL Server/PostgreSQL optional via connection strings/env vars.
- Test schema init and a minimal job flow on available providers.

### Constraints

- CI passes by default without external databases.
- No skipped tests counted as failures; category-gated.

### Suggested implementation path

- Use `[Category("Integration")]`.
- Read connections from environment.

### Acceptance criteria

- Provider matrix runs locally with configured connection strings.
- Default CI tests pass.
- Migration/schema tests cover all supported providers when available.

### Files likely involved

- `tests/Symphony.Tests/ProviderMatrixTests.cs`
- `tests/Symphony.Tests/IntegrationInfrastructure.cs`

---

## Task T50: Documentation and deployment guide

### Priority

Medium

### Milestone

M8

### Goal

Update documentation for setup, configuration, deployment, and first-run bootstrap.

### Scope

- README updates for web product.
- Document provider switching, connection strings, encryption, docker compose, first admin.
- Document API submission and curl usage.
- Document workflows and BYOM configuration.

### Constraints

- Remove proof-of-concept workflow-file mode instructions.
- Keep docs aligned with actual app settings.

### Suggested implementation path

- Update `README.md`.
- Add `docs/` deployment notes if needed.

### Acceptance criteria

- A new developer can run app via compose and log in.
- Provider and env vars documented.
- Curl example works as documented.

### Files likely involved

- `README.md`
- `docs/plans/SYMPHONY-WEB.md`
- `docker-compose.yml`

---

## Task T51: First-cut hardening and legacy polling removal

### Priority

High

### Milestone

M8 (decision gate)

### Goal

Remove legacy candidate polling from the shipped web path and apply final hardening.

### Scope

- Remove/leave unused `OrchestratorService` candidate polling from web app.
- Remove `FetchCandidateIssuesAsync` from normal job path.
- Verify Copilot-only registration.
- Apply CSRF protection for admin forms.
- HTTPS/HSTS/secure cookies in production.
- Prompt footer? N/A.
- Confirm no Codex runtime exposure.

### Constraints

- Retain Codex source code for future use.
- Do not break DapploJira tests.

### Suggested implementation path

- Delete hosted service registration.
- Keep interface method for tracker compatibility.
- Add anti-forgery tokens on forms.

### Acceptance criteria

- Web app has no candidate polling.
- No Codex agent is registered/exposed.
- Admin forms validate anti-forgery.
- Build/tests pass.

### Files likely involved

- `src/Symphony.App/Orchestration/OrchestratorService.cs`
- `src/Symphony.App/Program.cs`
- `src/Symphony.App/Pages/**/*.cshtml`
- `src/Symphony.App/IssueTracker/IIssueTracker.cs`

---

## Task T52: Release verification checklist

### Priority

High

### Milestone

M8

### Goal

Run a final verification pass across build, tests, docker, and milestone scenarios.

### Scope

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests`
- Docker compose smoke.
- M1-M6 manual scenario validation.
- Capture known issues/remaining open decisions.

### Constraints

- No implementation changes unless fixing discovered defects.
- Record results.

### Suggested implementation path

- Run tests with default provider.
- Run with PostgreSQL if available.
- Walk through milestones.

### Acceptance criteria

- Build and default tests pass.
- Docker smoke passes.
- All M1-M6 scenario steps documented as passed or with explicit remaining issues.
- Final first-cut readiness statement.

### Files likely involved

- `Symphony.slnx`
- `docs/plans/SYMPHONY-WEB-PLAN.md`

---

## Additional tasks (repeat as needed)

If a coding agent discovers a focused gap, add a new task using the same structure. Keep behavior changes separate from docs-only cleanup where practical, and separate design-only investigation from implementation work.

## Final checklist

- every task has a clear owner-sized scope
- every task has acceptance criteria
- decision-gate tasks are clearly marked
- likely files are listed to reduce agent search time
- execution order reflects real dependencies
- milestones M1-M8 map to concrete capabilities
- no implementation has been started by this document