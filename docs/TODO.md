# TODO: issue #23 — cookie auth must not redirect API endpoints to login

## Problem

Issue #23: starting with ASP.NET Core 10, unauthenticated requests to known API endpoints return
401/403 instead of redirecting to the cookie login page. Detection is based on endpoint metadata
(`IDisableCookieRedirectMetadata`) that .NET 10 adds only to `[ApiController]` endpoints, minimal API
endpoints that read JSON bodies or write JSON responses, `TypedResults` handlers, and SignalR hubs.

Live probe of the current host (branch base `2ebba47`, .NET 10.0.13, disposable SQLite):

| Request | Actual | Expected |
| --- | --- | --- |
| `GET /api/api-keys/` | 302 -> `/Account/Login` | 401 |
| `POST /api/api-keys/{id}/rotate` | 302 -> `/Account/Login` | 401 |
| `DELETE /api/api-keys/{id}` | 302 -> `/Account/Login` | 401 |
| `POST /api/api-keys/` (JSON body) | 401 | 401 |
| `PATCH /api/api-keys/{id}` (JSON body) | 401 | 401 |
| `POST /hubs/jobs/negotiate` | 401 | 401 |
| `GET /api/workflows/`, `/api/jobs/`, `/api/configuration/` | 401 | 401 |
| `GET /admin`, `/`, `/Account/ApiKeys` | 302 -> `/Account/Login` | 302 |

Root cause: `src/Symphony/Api/ApiKeyEndpoints.cs:10` is the only group using `RequireAuthorization()`.
Its GET/rotate/DELETE handlers return `Task<IResult>` and take no JSON body, so .NET 10 does not infer
the disable-redirect metadata. The cookie challenge therefore still issues a 302. POST/PATCH take a
JSON body, so they were detected and already return 401.

The other `/api` groups (`ConfigurationEndpoints`, `JobEndpoints`, `WorkflowEndpoints`, `WorkEndpoints`)
do not use `RequireAuthorization()` today; their handlers call `Results.Unauthorized()` directly. They
pass today, but any future `RequireAuthorization()` on those groups would regress the same way.

Issue #23 also references `docs/plans/SYMPHONY-WEB.md` tasks T4/T13/T16/T22/T32/T39. That file (and the
whole `docs/plans/` folder) was deleted in `db497c3` / `b1145e8`, so there is no plan document to
update. The invariant goes into living docs instead.

## Acceptance criteria

1. Unauthenticated calls to known API endpoints return 401 or 403, not a login redirect.
2. Unauthenticated browser requests to protected MVC screens still redirect to `/Account/Login`.

## Proposed changes

### 1. Endpoint conventions (the fix)

Add `.DisableCookieRedirect()` to every `/api` group so challenges/forbids produce 401/403
regardless of body inference, and so future `RequireAuthorization()` calls stay correct:

- `src/Symphony/Api/ApiKeyEndpoints.cs:10` — `MapGroup("/api/api-keys")`
- `src/Symphony/Api/ConfigurationEndpoints.cs:13` — `MapGroup("/api/configuration")`
- `src/Symphony/Api/JobEndpoints.cs:13` — `MapGroup("/api/jobs")`
- `src/Symphony/Api/WorkflowEndpoints.cs:11` — `MapGroup("/api/workflows")`
- `src/Symphony/Api/WorkEndpoints.cs:12` — `MapPost("/api/work", ...)`

No new `using` is needed: `CookieRedirectEndpointConventionBuilderExtensions` lives in
`Microsoft.AspNetCore.Builder`, already covered by Web SDK implicit usings.

### 2. Explicitly unchanged

- `src/Symphony/Data/IdentityRegistration.cs` cookie options stay as they are, so MVC page challenges
  keep redirecting to `/Account/Login` and `/Account/AccessDenied`.
- Do not set the app-wide `Microsoft.AspNetCore.Authentication.Cookies.IgnoreRedirectMetadata` switch
  and do not override `OnRedirectToLogin` / `OnRedirectToAccessDenied`.
- OTLP ingest (`/v1/*`, `/otlp/v1/*`) stays anonymous by design; agents post telemetry without a user
  cookie.
- 401/403 responses still carry the `Location` header; that is framework behavior and only the status
  code changes. Clients branch on status code.

### 3. Tests

New `tests/Symphony.Tests/Api/ApiCookieRedirectTests.cs`, `[TestFixture, Category(TestCatory.Unit)]`,
unit-level only (ADR-0002 forbids `WebApplicationFactory` smoke tests in `Symphony.Tests`):

- `MapApiEndpoints_AllApiRoutes_CarryDisableCookieRedirectMetadata` — build endpoints from the real
  `Map*Endpoints()` extension methods in a bare `WebApplication.CreateBuilder()`; stub-register handler
  services (`ApiKeyService`, `JobService`, `ConfigurationService`, `WorkflowService`) so
  `RequestDelegateFactory` treats them as services instead of inferred JSON bodies. Assert every route
  starting with `/api` has `IDisableCookieRedirectMetadata`, and that the endpoint set is non-empty.
- `MapControllerRoute_ControllerEndpoints_DoNotCarryDisableCookieRedirectMetadata` — map the MVC
  controller route and assert controller endpoints do not have the metadata, pinning acceptance
  criterion 2 (pages keep redirecting).

Fallback if materializing minimal-API endpoints in a test process proves brittle: assert the convention
metadata through a test-double `IEndpointRouteBuilder`. Escalate rather than weaken the assertion.

### 4. Docs

- `docs/ARCHITECTURE.md` — Identity/Auth section: API challenges return 401/403 without a redirect
  (`Location` still present; branch on status code); MVC screens redirect to `/Account/Login`; admin
  denial to `/Account/AccessDenied`.
- `README.md` — key-behavior bullets in "Submitting Work With curl (API Keys)".
- `GETTING-STARTED.md` — same bullet in its API section.

## Blast radius

- `src/Symphony/Api/*.cs` (5 files): endpoint metadata only. No handler logic, DTOs, routes, status
  codes for authenticated calls, rate limiting, or auth middleware change.
- Cookie authentication configuration is untouched, so MVC redirect behavior is unchanged; the
  existing `IdentityRegistrationTests` continue to pin `LoginPath`/`LogoutPath`/`AccessDeniedPath`.
- `Program.cs`, `IdentityRegistration.cs`, `ApiKeyMiddleware.cs`, `JobUpdateHub.cs` unchanged.
- New test file only; no existing test edited.
- Downstream: CLI/curl clients that followed a 302 to an HTML login page now receive 401. That is the
  intended change from the issue. README/GETTING-STARTED document it.

## Verification

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Live matrix on a disposable SQLite file (`ASPNETCORE_URLS=http://localhost:5099`,
  `ConnectionStrings__Sqlite` under `%TEMP%`): all `/api/*` -> 401 with no redirect followed,
  `/admin`, `/`, `/Account/ApiKeys` -> 302 to `/Account/Login`, `/hubs/jobs/negotiate` -> 401.
- After the PR: rebuild the Docker image and run the same matrix (plus Playwright MCP browser checks
  for the redirect behavior) against the container.

## Planned commits

1. `docs: plan issue 23 API cookie redirect fix in TODO.md`
2. `fix(api): disable cookie redirect for API endpoints` (5 endpoint files)
3. `test(api): pin API 401 and MVC redirect endpoint conventions`
4. `docs: document API 401/403 vs MVC login redirect behavior`
5. `docs: remove TODO.md - plan executed`

## GitHub issues log

Reminder: as each task executes, create an issue immediately via
`gh issue create --repo khurram-uworx/Symphony` for any deferred work or concern found, and record the
number here. Do not rely on memory; compaction can lose it.

- [ ] #116 — API 401/403 responses still carry a `Location` header to the login page (found while verifying #23; framework behavior, documented, decision pending)
