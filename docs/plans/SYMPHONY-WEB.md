# Symphony Web — Implementation Plan

This document captures the end-to-end implementation plan for reimagining Symphony as a full-fledged, multi-user, database-backed web application.

The earlier console/polling/`WORKFLOW.md`-file mode is proof-of-concept only and is not retained. The web application is the product.

## 1) Product model

Symphony becomes a **multi-user web application plus API plus background processor**.

- Work is always **explicitly submitted**. There is **no polling for candidate work**.
- Users are created by admins (admin-invited); there is no public self-registration in the first cut. Users log in after an admin creates their account.
- Each user has a saved **Configuration**:
  - Work management tool: Linear or Jira (GitHub placeholder for future)
  - Tracker settings: endpoint, credentials, project/slug, active/terminal states
  - Agent: Copilot SDK in the first cut. Codex code is retained in the repository for future use, but it is not registered or exposed by the web product.
  - Copilot BYOM settings: provider type, base URL, model ID, API key/secrets, and any provider-specific options
  - Workspace base root. Runtime jobs run in a user-scoped folder beneath it (for example `/data/{userId}`), preventing cross-user workspace collisions.
  - Hook preferences, max turns, and per-user concurrency.
  - Default workflow name (optional, used for UI preselection)
- Each user can maintain multiple named **Workflows**. A workflow is a WORKFLOW.md-style document containing front matter and a prompt template ("master instruction"). The first workflow is always named `WORKFLOW` and is seeded from the bundled existing `WORKFLOW.md`. Users may rename, copy, edit, or delete workflows, but at least one workflow must always remain.
- When only ticket IDs are submitted, the user's configured tracker is used as a **context-fetching tool** to resolve full issue details.
- Every job is owned by a user.
- Users see only their own jobs; admins can see and manage all jobs.
- Each job request explicitly specifies the target GitHub repository, GitHub Personal Access Token, and workflow name to use. The PAT and selected workflow/runtime inputs are request-scoped; the PAT is never part of the saved user configuration.

## 2) Database and persistence

Use Entity Framework Core with **SQLite by default**, while supporting **SQL Server** and **PostgreSQL** through configuration, following the CodeMemory pattern: one DbContext, provider-specific EF Core providers, provider-specific connection strings, and startup initialization that works for each provider.

Supported database providers:

- `sqlite` (default): `Microsoft.EntityFrameworkCore.Sqlite`; local file database, e.g. `Data Source=App_Data/symphony.db`.
- `sqlserver`: `Microsoft.EntityFrameworkCore.SqlServer`; requires `ConnectionStrings:SqlServer`.
- `postgresql` (aliases: `postgres`, `npgsql`): `Npgsql.EntityFrameworkCore.PostgreSQL`; requires `ConnectionStrings:PostgreSQL` (`ConnectionStrings:Npgsql` is accepted as an alias).

The active provider is selected from `Database:Provider` and passed into `AddDbContext`/`AddDbContextFactory`. Do not hard-code a single provider in application startup.

### Entities

Identity:
- `IdentityUser`
- `IdentityRole`

Symphony:
- `UserConfiguration` (1:1 with user)
  - Tracker kind
  - Tracker endpoint
  - Tracker credentials (encrypted at rest)
  - Project/slug
  - Active states
  - Terminal states
  - Agent type (Copilot SDK in first cut)
  - Agent configuration JSON (Copilot BYOM provider settings and secret references)
  - Workspace base root (runtime jobs run in a user-scoped subfolder)
  - Hooks
  - Default workflow name (optional)
  - Max turns
  - Max concurrent issues
  - Created/updated timestamps
- `Workflow` (many per user)
  - `Id` (Guid)
  - `OwnerUserId`
  - `Name` (unique per owner, e.g. `WORKFLOW`, `develop-web-app`; document behavior for rename vs. jobs that reference the name)
  - `Content` (full WORKFLOW.md-style markdown, including YAML front matter and master instruction)
  - `IsDefault` (optional convenience flag)
  - `CreatedAt`
  - `UpdatedAt`
- `Job`
  - `Id` (Guid)
  - `ExternalId` (optional)
  - `OwnerUserId`
  - `WorkflowName` (required selected workflow name)
  - `RetryOfJobId` (optional; set when an admin retry creates a new linked job)
  - `Repository` (requested target GitHub repository, non-secret)
  - `EncryptedRequestSnapshot` (optional, encrypted full request snapshot used for audit/reproducibility)
  - `State` (`Queued`, `Running`, `Completed`, `CompletedWithFailures`, `Failed`, `Cancelled`)
  - `QueuedAt`
  - `StartedAt`
  - `CompletedAt`
  - `SummaryJson`
  - `ErrorCode`
  - `ErrorMessage`
- `JobIssue`
  - `Id` (Guid)
  - `JobId`
  - `IssueId`
  - `Identifier`
  - `Title`
  - `State`
  - `Success`
  - `Reason`
  - `TurnCount`
  - `StartedAt`
  - `CompletedAt`
  - `WorkspacePath`
- `JobEvent`
  - `Id`
  - `JobId`
  - `JobIssueId` (optional)
  - `Timestamp`
  - `EventName`
  - `Summary`
  - `PayloadJson`
- `ApiKey` (user-owned)
  - `Id`
  - `Name`
  - `Prefix`
  - `HashedKey`
  - `OwnerUserId` (required; API keys are owned only by users)
  - `IsEnabled` (default true)
  - `CreatedAt`
  - `ExpiresAt` (optional)
  - `RevokedAt` (optional)
  - `LastUsedAt` (optional)
- `AppSetting`
  - `Key`
  - `Value`

### Indexing

- `Job(State, QueuedAt)`
- `Job(OwnerUserId, QueuedAt)`
- `JobIssue(JobId)`
- `JobEvent(JobId, Timestamp)`
- `ApiKey(Prefix)`
- `Workflow(OwnerUserId, Name)` unique

### Provider switching (CodeMemory-style EF Core registration)

Register the DbContext with a provider selected at runtime:

```csharp
var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "sqlite";

(string? connectionString, string normalizedProvider) = dbProvider.ToLowerInvariant() switch
{
    "sqlite" => (builder.Configuration.GetConnectionString("Sqlite")
        ?? "Data Source=App_Data/symphony.db", "sqlite"),
    "sqlserver" => (builder.Configuration.GetConnectionString("SqlServer")
        ?? throw new InvalidOperationException("Connection string 'SqlServer' is required for Database:Provider 'sqlserver'"), "sqlserver"),
    "postgresql" or "postgres" or "npgsql" => (builder.Configuration.GetConnectionString("PostgreSQL")
        ?? builder.Configuration.GetConnectionString("Npgsql")
        ?? throw new InvalidOperationException("Connection string 'PostgreSQL'/'Npgsql' is required for Database:Provider 'postgresql'"), "npgsql"),
    var p => throw new InvalidOperationException($"Unsupported database provider '{p}'")
};

builder.Services.AddDbContextFactory<SymphonyDbContext>(options =>
{
    switch (normalizedProvider)
    {
        case "sqlite":
            options.UseSqlite(connectionString);
            break;
        case "sqlserver":
            options.UseSqlServer(connectionString);
            break;
        case "npgsql":
            options.UseNpgsql(connectionString);
            break;
    }
});
```

CodeMemory additionally creates the SQLite parent directory during startup; Symphony should do the same before `EnsureCreated`/migrations when `Database:Provider = sqlite`.

### Migrations and schema initialization

- SQLite: development can use `EnsureCreated`; production can use EF Core migrations for the SQLite provider.
- SQL Server and PostgreSQL: use EF Core migrations, ideally provider-specific migration assemblies or folders because migration SQL is provider-specific.
- Do not assume one migration set is portable across SQLite, SQL Server, and PostgreSQL.
- Postgres-specific features (such as case-insensitive collations, JSON operators, or vector search) should be isolated behind provider checks or dedicated storage providers rather than leaking into core entity mappings.
- Seed roles/users/settings after schema initialization, regardless of provider.

### Startup

- Initialize/verify the database for the configured provider before seeding:
  - SQLite: create the database directory, then run `EnsureCreated` in development or migrations in production.
  - SQL Server/PostgreSQL: run provider-specific migrations or an idempotent schema initialization.
- Handle `Running`/stale jobs and other startup recovery after schema init.
- Seed roles: `Admin` and `User`.
- Seed the first admin user only when no user with the `Admin` role exists:
  - default username: `testadmin` (configurable, e.g. `SeedAdmin:UserName`)
  - generate a cryptographically random password only on this first boot
  - print the generated username/password to the console exactly once
  - if any admin account already exists, skip seeding and do not print or overwrite credentials
- Seed the bundled default workflow named `WORKFLOW` (content imported from the existing `WORKFLOW.md`) for every user that has none, including on new-user creation.
- Seed default `AppSetting` values (global concurrency, workspace root, max retry backoff, etc.).

## 3) Authentication and authorization

### Admin web UI

- Use ASP.NET Core Identity with cookie authentication.
- Identity UI can be used for first cut, or later replaced with custom pages.
- Require role `Admin` for all `/admin/*` routes.
- `/admin/users` lets admins create and manage users in `Admin` and `User` (member) roles:
  - create/disable/delete accounts
  - assign or change roles
  - reset credentials as needed
- Disabling or deleting a user invalidates that user's sessions and API keys.
- `/account/api-keys` lets the signed-in user manage their own X-API-KEY(s):
  - create a system-generated key
  - rotate an existing key (revoke old, issue new)
  - enable or disable a key
  - view metadata only (prefix, name, enabled state, created/expires dates)
  - raw key is shown only once at creation/rotation

Admins do not manage users' personal API keys.

### API

- Support Identity cookie authentication for browser/API calls.
- Support `X-API-KEY` for programmatic access.
- X-API-KEY is owned and controlled by the user it belongs to:
  - each member (and admin) can manage only their own keys
  - admins manage users, roles, and app settings, not another user's personal keys
  - a key is usable only when it exists, its hash matches, it is enabled, and it is not expired or revoked
  - requests are attributed to the key owner
- API key values are hashed in the database. Show the raw key only once at creation.

### Authorization rules

- Normal users can submit jobs and read/mutate only their own jobs.
- Admins can view all jobs, cancel any job, retry jobs, and manage users/settings.
- Job ownership is determined by `Job.OwnerUserId`.

## 4) Per-user configuration flow

1. User logs in.
2. User opens `/account/configuration`.
3. User saves:
   - tracker kind, endpoint, credentials, project/slug, state settings
   - agent type (Copilot SDK in the first cut) and Copilot BYOM configuration
   - workspace root
   - hooks
   - max turns
   - max concurrent issues
4. Copilot BYOM configuration includes:
   - provider type (`openai`, `azure`, or `anthropic`)
   - provider base URL
   - model ID, used for both provider and session configuration
   - provider API key or bearer-token callback configuration (encrypted at rest)
   - optional OpenAI wire API (`completions` or `responses`) where applicable
5. System stores this as `UserConfiguration`.
6. Tracker credentials, Copilot provider secrets, and other secrets are encrypted at rest.
7. Runtime jobs build `ServiceConfig` from the owner's saved configuration.
8. GitHub PAT and target repository are supplied with each request and are not part of `UserConfiguration`.
9. Optional per-request overrides may be allowed for a single run.
10. The optional default workflow name is a convenience value for UI preselection only; it does not make the API accept an omitted workflow name.

## 5) Work execution model

Work is always explicitly submitted. There is no candidate polling and no background queue discovery by state.

### Supported submission styles

- `ticketIds`: tracker-native references such as `PROJ-123`
- `workItems`: fully supplied work items
- mixed submission: some fetched tickets and some inline items

- A job stores the selected workflow name, not a copy of workflow content. Workflow content is resolved by name when the job runs.
- Admin retries create a new linked job rather than mutating the original job.

If ticket IDs are supplied, the system uses the owner's configured tracker to fetch those ticket details. This is a context/tool operation, not polling.

### Processing sequence

1. Receive explicit submission, including the target GitHub repository, GitHub PAT, and workflow name for that request.
2. Validate that at least one ticket ID or work item exists.
3. Validate that the request includes a GitHub repository, PAT, and an existing owned workflow name.
4. Load the named workflow and use its content as the master instruction/prompt template. Workflow front matter may override run-level configuration such as workspace, hooks, and agent turn limits.
5. If needed, fetch ticket details through the owner's configured tracker.
6. Map fetched tickets to `Domain.Issue`.
7. Build runtime `ServiceConfig`, injecting the owner's Copilot BYOM configuration.
8. Create the agent with Copilot SDK BYOK settings (provider type, base URL, model ID, API key/secret) from the owner's configuration.
9. Run the agent per issue with retry and concurrency controls, using the request's GitHub PAT/repository for repository operations.
10. Refresh ticket state by ID during the run when a tracker is configured.
11. Persist issue results and telemetry events. Never persist a plaintext GitHub PAT; any persisted snapshot may contain only an encrypted PAT and must not surface in events, logs, or status responses.
12. Mark the job final state.

### Important existing behavior to preserve

- `AgentRunner.RunAttemptAsync` already handles:
  - workspace creation
  - before/after run hooks
  - turn loop
  - state refresh by ID
  - shutdown and cleanup
- The new system should reuse this.
- Candidate discovery (`FetchCandidateIssuesAsync`) is not used by the new web product. For inline work, use `NullIssueTracker`.

## 6) Service configuration factory

Create `ServiceConfigFactory.Build(...)` from a user configuration plus request data.

It should construct:

- `TrackerConfig`
  - `Kind`
  - `Endpoint`
  - `ApiKey`
  - `ProjectSlug`
  - `ActiveStates`
  - `TerminalStates`
- `WorkspaceConfig`
- `HooksConfig`
- `AgentConfig`
  - max concurrent issues
  - max retry backoff
  - max turns
  - per-state concurrency
- `CopilotConfig` (first cut)
  - Copilot SDK provider type
  - provider base URL
  - model ID
  - secret reference or encrypted secret for API key or bearer-token provider
  - optional OpenAI wire API
  - request-scoped GitHub repository and PAT are supplied separately by the job runner and are not part of saved configuration
- `WorkflowDefinition`
  - Config map from the selected workflow front matter, potentially overriding values from `UserConfiguration`
  - Prompt template parsed from the selected named workflow content (the master instruction)

### Tracker endpoint defaults

- Linear: `https://api.linear.app/graphql`
- Jira: require endpoint, or use sensible default where appropriate later

### Posted work without tracker

If work is supplied inline, use a `NullIssueTracker`.

The active states should auto-include the submitted work item states so the agent runs to the configured turn limit rather than exiting immediately.

## 7) API contract

### Core endpoints

- `POST /api/work`
  - authenticated by cookie or API key
  - body:
    - `externalId` (optional)
    - `ticketIds` (optional)
    - `workItems` (optional)
    - `github` (required)
      - `repository` (required; owner/name or URL form as defined by implementation)
      - `personalAccessToken` (required; used only at runtime, encrypted in any persisted snapshot, never returned)
    - `workflowName` (required; must name an existing workflow owned by the requester)
    - `overrides` (optional)
  - validation: at least one ticket ID or work item; repository and PAT are required
  - queue behavior: create job with `State = Queued`
  - response: `202 { jobId, statusUrl }`

- `GET /api/jobs`
  - users: only owned jobs
  - admins: all jobs, with filters

- `GET /api/jobs/{id}`
  - returns owned job and issue results, or any job for admins

- `DELETE /api/jobs/{id}`
  - cancel owned job, or any job for admins

- `GET /api/configuration`
  - get current user's configuration

- `PUT /api/configuration`
  - update current user's configuration

- `GET /health`
  - basic health check

### Additional endpoints

User-owned workflows:
- `GET /api/workflows` — list current user's workflows (metadata and content)
- `GET /api/workflows/{name}` — get one workflow
- `POST /api/workflows` — create a named workflow
- `PUT /api/workflows/{name}` — update a workflow
- `DELETE /api/workflows/{name}` — delete a workflow (cannot delete the last remaining workflow for that owner)

User-owned API keys:
- `GET /api/api-keys` — list current user's keys (metadata only)
- `POST /api/api-keys` — create a system-generated key; return raw key once
- `POST /api/api-keys/{id}/rotate` — rotate a key; return new raw key once
- `PATCH /api/api-keys/{id}` — enable or disable a key
- `DELETE /api/api-keys/{id}` — revoke a key

Admin endpoints:
- user and role management (`/admin/users` backing APIs)
- app settings updates
- job retry endpoints, e.g. `POST /api/jobs/{id}/retry` (admin only; retry creates a new job linked to the original via `RetryOfJobId`)

### Status DTO

```json
{
  "id": "guid",
  "externalId": "optional",
  "owner": "user alias",
  "workflowName": "WORKFLOW",
  "state": "Queued|Running|Completed|CompletedWithFailures|Failed|Cancelled",
  "queuedAt": "...",
  "startedAt": "...",
  "completedAt": "...",
  "tracker": {
    "kind": "linear",
    "projectSlug": "..."
  },
  "agent": {
    "type": "copilot"
  },
  "issues": [
    {
      "issueId": "...",
      "identifier": "PROJ-1",
      "title": "...",
      "success": true,
      "reason": null,
      "turnCount": 4,
      "startedAt": "...",
      "completedAt": "..."
    }
  ],
  "error": null
}
```

## 8) Queue and job processing

Use a database-backed queue. In-memory channels may be used only as a wake-up signal.

### Job processor behavior

- Background `JobProcessor` runs as a hosted service.
- On startup:
  - recover `Queued` jobs
  - handle stale `Running` jobs according to recovery policy
- Poll the database for queued jobs respecting global concurrency.
- For each job:
  - mark `Running`
  - resolve owner configuration
  - resolve issue list
  - sort issues by priority then created date
  - run per issue with retry
  - persist issue status and agent events
  - mark final job state

### Per-issue run

For each attempt:

- create fresh agent instance
- create issue tracker from owner config if needed
- create `WorkspaceManager` backed by owner config
- create `AgentRunner`
- call `RunAttemptAsync`
- on success, stop retries
- on failure, retry with exponential backoff up to `maxRetries`
- persist `JobIssue` and `JobEvent` records

### Cancellation

- `DELETE /api/jobs/{id}` cancels the job CTS.
- Processor catches cancellation and marks job `Cancelled`.
- CTS should be linked to application stopping token.

### Recovery policy

Recommended: mark stale `Running` jobs as `Failed` and emit an interrupted-run event. Alternative: requeue them.

### Concurrency and workspace isolation

- Per-user limits are primary for scheduling a user's issues.
- Global concurrency remains a process-wide safety ceiling for hosted job processing.
- Per-tracker-state limits from agent configuration are applied when dispatching individual issues.
- Each user's jobs run under a user-scoped workspace folder derived from that user's configured workspace base root.
- Workspaces are never shared between users.

## 9) Web UI (admin and member pages)

Recommended first UI implementation: Razor Pages. Blazor Server can be evaluated later.

### Pages

- `/admin`: dashboard with counters and recent jobs
- `/admin/jobs`: filterable, paginated job list
- `/admin/jobs/{id}`: job detail with issue timeline and event tail
- `/admin/users`: user and role management
- `/admin/settings`: concurrency, default settings, workspace root
- `/account/api-keys`: create, rotate, enable, and disable personal X-API-KEYs
- `/account/workflows`: create, edit, rename, and delete named workflows; includes the default `WORKFLOW` for newly created users
- `/account/run`: friendly member-facing request launcher that submits the same payload as `POST /api/work`. This page uses a tabbed view:
  - **Run**: form for ticket IDs/work items, GitHub repository, PAT, workflow selection, and optional overrides, then submit via the same API path.
  - **API**: live documentation with a copy-pasteable `curl` example using the current user's actual enabled API key (or guidance to create one) and the selected/default workflow, so the same request can be replayed from a terminal. For users without an API key, the tab explains how to create/enable one.
- `/account/configuration`: personal work tool and agent setup

### Live updates

- Phase 1: polling every 3 to 5 seconds
- Phase 2: SignalR push notifications

## 10) Required refactors to existing code

### 1) Configuration provider abstraction

Introduce:

```csharp
interface IServiceConfigProvider
{
    ServiceConfig GetConfig();
}
```

- `ServiceConfigProvider` implements it.
- `StaticServiceConfigProvider` implements it for per-job runtime config.

`AgentRunner` and `WorkspaceManager` should depend on `IServiceConfigProvider` instead of the concrete provider.

### 2) Factories

- `IssueTrackerFactory.Create(ServiceConfig, submissionContext)` (or equivalent request-aware selection) so inline-only work uses `NullIssueTracker`.
- `CodingAgentFactory.Create(ServiceConfig)` creates the Copilot SDK agent only in the first cut.

These mirror the DI logic currently in `Program.cs`.

### 3) Null issue tracker

Create `NullIssueTracker` for inline posted work. It should:

- return input issues unchanged for `FetchIssueStatesByIdsAsync`
- return empty candidate issues
- allow refresh semantics to work without external state updates

### 4) Agent runner reuse

Keep `AgentRunner` mostly unchanged. Only change constructor parameter type to `IServiceConfigProvider`.

### 5) Remove candidate polling from the product

Do not carry candidate-polling orchestration into the web app. `FetchCandidateIssuesAsync` is not part of normal job processing. The legacy `OrchestratorService` candidate-polling implementation is not retained; remove it or leave it unused outside the shipped web path.

### 6) Remove Codex from first-cut runtime

Retain Codex source code, but do not register `CodexAgent`, do not expose it in configuration UI, and ignore Codex-related front matter during workflow parsing.

## 11) Project and entrypoint changes

- Change `src/Symphony.App/Symphony.App.csproj` SDK to `Microsoft.NET.Sdk.Web`.
- Add packages:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  - `Microsoft.EntityFrameworkCore.Sqlite`
  - `Microsoft.EntityFrameworkCore.SqlServer`
  - `Npgsql.EntityFrameworkCore.PostgreSQL`
  - `Microsoft.EntityFrameworkCore.Design`
  - optional: `Microsoft.AspNetCore.SignalR`
- Add provider selection:
  - default config value `Database:Provider = sqlite`
  - connection strings `Sqlite`, `SqlServer`, and `PostgreSQL`/`Npgsql`, matching CodeMemory's multi-provider registration style.
- Replace `Program.cs` with web application bootstrap:
  - select EF Core provider from configuration
  - configure DbContext factory for SQLite/SQL Server/PostgreSQL
  - create SQLite database directory when needed
  - run schema initialization/migrations before seeding
  - configure Identity
  - seed admin password to console
  - register queue and hosted processor
  - register endpoint mapping
  - apply API key middleware

### Product direction

This web application is the product. The earlier workflow-file polling mode is **not** retained for backward compatibility; it was proof-of-concept only. The `symphony --workflow WORKFLOW.md` entry point is removed rather than kept as a legacy mode.

## 12) Security

- First-admin bootstrap runs only when no admin account exists; generated password is printed once and never logged again.
- Admins manage user and role lifecycle. Admins do not manage users' personal API keys; disabling or deleting a user invalidates that user's access.
- GitHub PAT is request-scoped runtime input: validate presence, use only during job processing, and never store it in plaintext in database rows, job events, logs, or status responses. If request snapshots are stored, store it encrypted and exclude it from normal reads.
- Copilot BYOM secrets are user-scoped, encrypted at rest, and decrypted only in the owner's job process.
- Cookie options:
  - `Secure`
  - `HttpOnly`
  - `SameSite`
  - reasonable expiry with sliding renewal
- X-API-KEY comparison should be constant-time after prefix lookup and hash verification.
- A key must be enabled to be usable; disabled or revoked keys are rejected.
- API keys hashed in database.
- Secrets excluded from job status responses.
- Rate limiting for API endpoints.
- CSRF protection for admin forms.
- HTTPS enforcement in production.
- Persist the ASP.NET Core Data Protection key ring in a shared/location-appropriate store for multi-instance or provider-switched deployments.

## 13) Testing plan

- Unit tests:
  - `ServiceConfigFactory.Build`
  - redaction helpers
  - queue state transitions
  - first-admin bootstrap idempotency
  - API key middleware
  - API key enable/disable and rotation behavior
  - Copilot BYOM configuration validation (provider type, base URL, model ID)
  - request requires GitHub repository and PAT
  - encrypted handling/redaction of GitHub PAT in job snapshots/events
  - default `WORKFLOW` seeding from bundled content for new/existing users
  - named workflow CRUD and ownership
  - selected workflow front matter + prompt parsing
- Integration tests:
  - `WebApplicationFactory<Program>`
  - SQLite temporary database (default)
  - optional provider-matrix tests for SQL Server and PostgreSQL when connection strings are available
  - seeded admin and users
  - job submission and status flow
  - provider-specific schema initialization/migration tests
- Keep existing `DapploJiraTests`.
- Add per-user scoping tests.
- Add cancellation/recovery tests.

## 14) Implementation phases

1. Web scaffold + Identity + EF Core with SQLite/SQL Server/PostgreSQL provider selection + seeded admin password
2. Per-user `UserConfiguration` storage and settings UI/API
3. Explicit ticket submission + tracker context fetch + per-request GitHub repository/PAT handling
4. DB-backed job queue and processor persistence
5. Admin dashboard and job detail pages
6. API key management, app settings, permissions
7. SignalR live updates
8. Final hardening: encryption key management, deeper auditing, rate limiting, and provider-specific validation

## 15) Resolved decisions

1. User registration: admin-invited only. There is no public self-registration in the first cut.
2. Failure recovery: stale `Running` jobs are marked `Failed` and an interrupted-run event is emitted.
3. Secret storage: use ASP.NET Core Data Protection for field-level encryption now; vault integration remains a non-goal.
4. Job audit snapshots: store an encrypted full request snapshot for reproducibility.
5. Workflow reproducibility: a Job resolves workflow content by name at run time and does not snapshot workflow content.
6. Job retry: an admin retry creates a new linked job (`RetryOfJobId`) instead of mutating the original job.
7. User seeding: seed only the default `WORKFLOW` workflow for users that have none.
8. Concurrency: per-user limits are primary; global concurrency is a process-wide safety ceiling. Workspaces are user-scoped folders.
9. In-memory queue: database remains source of truth; in-memory channels are wake-up signals only.
10. Service accounts: not supported in the first cut. API keys are user-owned only.

### 15a) Remaining open decisions

- Optional per-request overrides: define the exact override allow-list and validation rules, including immutable keys, bounds, and audit requirements.
- Workflow identity vs. mutability: decide whether workflow names are immutable, whether renames cascade to job references, or whether jobs should store a `WorkflowId`.
- Workspace root safety: define validation for per-user base roots and reject traversal/relative paths.

## 16) Non-goals for first full release

- Kubernetes or container orchestration
- Cloud secret vault integration
- Full audit reporting
- GitHub tracker implementation beyond interface placeholders
- Advanced scheduling beyond global job concurrency
- Codex runtime support

## 17) Success criteria

The implementation is complete when:

- admin and users can log in
- default admin password prints once
- each user can save work tool and agent configuration
- each new user starts with a seeded `WORKFLOW` workflow and can create additional named workflows
- API submission works with cookie and API key auth
- API submission always requires an explicit workflow name
- ticket IDs can be resolved to issue context
- inline work submission works
- jobs run without candidate polling
- dashboard shows owned/all jobs correctly
- work survives server restart without polling for new candidates