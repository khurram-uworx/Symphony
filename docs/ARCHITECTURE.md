# Architecture

Design rationale and runtime shape for the Symphony .NET web host. For feature inventory, see [FEATURES.md](FEATURES.md).

## Product Model

Symphony is a multi-user ASP.NET Core MVC web application with a minimal API and a background job processor. The earlier console/polling/WORKFLOW.md-file mode is not retained; the web application is the product.

- Work is always explicitly submitted; there is no polling for candidate work.
- Users are created by admins (admin-invited); there is no public self-registration in the first cut.
- Each user has a saved UserConfiguration (tracker, Copilot BYOM, workspace root, hooks, limits, default workflow). Tracker credentials and Copilot secrets are encrypted at rest.
- Each user maintains named Workflow documents (WORKFLOW.md-style with front matter and prompt template). The default workflow is named WORKFLOW and seeded from the bundled WORKFLOW.md. At least one workflow must remain per user.
- When ticket IDs are submitted, the owner's configured tracker is used as a context-fetching tool (not for polling). Inline work uses NullIssueTracker.
- Jobs are owned by users; users see only their own jobs, admins see all jobs.
- Each submission specifies target GitHub repository, GitHub PAT, and workflow name. The PAT and workflow selection are request-scoped and never stored in plaintext in saved configuration.

## Folder Structure

```text
src/Symphony/                    # Web host
  Program.cs                    # ASP.NET Core entry point
  Controllers/                  # MVC controllers
  Views/                        # MVC views
  Api/                          # Minimal API endpoints
  Agent/                        # Copilot SDK and Codex runners
  Config/                       # ServiceConfig and factories/providers
  Data/                         # DbContext, migrations, seeding
  Domain/                       # Entities
  Hubs/                         # SignalR hubs
  IssueTracker/                 # Linear and Jira
  Middleware/                   # API key/auth middleware
  Processing/                   # JobQueue, JobProcessor
  Security/                     # Secret protection/redaction
  Services/                     # Application services
  Workflows/                    # Workflow loading/bundling
  Workspaces/                   # Workspace management
  Utils/
tests/
  Symphony.Tests/               # NUnit suite (CI)
  Symphony.CodingAgentsProbe/   # Manual Copilot SDK probes (not CI)
  Symphony.PerformanceTests/    # Manual performance harness (not CI)
docs/
  CONFIG.md
  adr/                          # Accepted architecture decisions
```

## Runtime Shape

- Program.cs configures MVC, Identity, database, data protection, secret protection, queue, processor, SignalR, rate limiting, and API endpoints.
- Kestrel serves the MVC app (default http://localhost:5080 via launchSettings.json).
- Startup applies provider-specific EF Core migrations via DatabaseInitializer, then seeds roles/admin and default workflows.

## Database and Persistence

- EF Core with SQLite by default; SQL Server and PostgreSQL supported via Database:Provider.
- Connection strings: Sqlite, SqlServer, PostgreSQL (aliases postgres, npgsql). Provider-specific migrations in Symphony.Migrations.* projects.
- Core entities: IdentityUser/IdentityRole, UserConfiguration, Workflow, Job, JobIssue, JobEvent, ApiKey (user-owned), AppSetting.
- Indices: Job(State,QueuedAt), Job(OwnerUserId,QueuedAt), JobIssue(JobId), JobEvent(JobId,Timestamp), ApiKey(Prefix), unique Workflow(OwnerUserId,Name). Workflows resolved by name at runtime; admin retries create linked jobs (RetryOfJobId).

## Identity, Authentication, and Authorization

- ASP.NET Core Identity with cookie auth. Custom login/logout on AccountController (no Identity UI/Razor Pages).
- API key auth via X-API-Key or Authorization: Bearer through ApiKeyMiddleware; keys user-owned, hashed, support enable/disable/rotate/revoke, raw value shown only once.
- Role Admin required for /admin/*; users manage only their own resources; admins can view/cancel/retry any job and manage users/settings. Disabling/deleting a user invalidates sessions and API keys.

## Job Processing

- Explicit submission via POST /api/work (requires ticketIds/workItems, github.repository, workflowName). GitHub personal access token may be supplied in the request or configured once on the Configuration page. Creates queued job with encrypted snapshot.
- JobQueue provides wake-up signals; database remains source of truth. JobProcessor (hosted service) recovers queued/stale jobs, claims with concurrency (per-user primary, global ceiling), processes per issue with retry, persists issues/events, marks terminal states.
- Per job, JobProcessor builds a `RepositoryConfig` (repository, job token, resolved git identity) and passes it through `ServiceConfig`. `WorkspaceManager` owns the checkout: create directory, run `hooks.after_create`, then clone shallowly if `.git` is missing. `GitHubEnvironment` is the single place that turns that config into git/gh environment variables for hooks, the workspace, and the agent session.
- Cancellation via DELETE /api/jobs/{id} sets Cancelled. Stale Running jobs become Failed with interrupted-run events on startup.
- Trackers fetched only when ticket IDs supplied; inline work uses NullIssueTracker. Copilot SDK is first-cut agent (Codex retained but not registered/exposed).

## Web UI and APIs

- MVC Razor views/controllers; minimal APIs under /api (configuration, workflows, work, jobs, api-keys). SignalR hub at /hubs/jobs.
- Screens: /admin (dashboard/jobs/users/settings), /account (index/configuration/workflows/run/api-keys). /account/run provides form submission and curl example using user's enabled API key.
- Status DTOs mask secrets/snapshots. Rate limiting on APIs; antiforgery on admin forms.

## Data Protection and Secrets

- ISecretProtector using ASP.NET Core Data Protection for field-level encryption. Data Protection key ring persisted when configured.
- Cookie settings hardened in production (Secure/HttpOnly/SameSite, HTTPS/HSTS). GitHub PAT and tracker/Copilot secrets encrypted at rest; never logged or returned in normal API responses.

## Testing

- tests/Symphony.Tests is the CI suite (NUnit). Excluded: Integration, Smoke, ReleaseReadiness by default.
- CI builds/tests Symphony.Tests, collects Cobertura via coverlet.collector, fails if no coverage artifact.
- Playwright checks via Playwright MCP against a launched host (not in Symphony.Tests). Integration tests requiring external services need env vars.
