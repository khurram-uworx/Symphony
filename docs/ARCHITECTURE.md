# Architecture

Design rationale and runtime shape for the Symphony .NET web host. For feature inventory, see [FEATURES.md](FEATURES.md).

## Runtime Shape

- `src/Symphony/Program.cs` configures MVC, Identity, database, data protection, secret protection, queue, and processor.
- Kestrel serves the MVC app on `http://localhost:5080` with `launchSettings.json`.
- Startup applies provider-specific EF Core migrations, then seeds admin.

## Database Providers

- Provider selected by `Database:Provider`.
- SQLite uses `Symphony.Migrations.Sqlite`; SQL Server uses `Symphony.Migrations.SqlServer`; PostgreSQL uses `Symphony.Migrations.PostgreSQL`.
- Each migration set is provider-specific; update all three together.
- `DatabaseInitializer` creates parent directories for SQLite.

## Identity and Authorization

- ASP.NET Core Identity cookie auth.
- Minimal API and MVC actions resolve user from `ClaimsPrincipal`; admin checks use `IsInRole("Admin")`.
- User and role management is admin-only.

## Job Processing

- `WorkEndpoints` creates `Job` rows with encrypted snapshots.
- `JobQueue` is a wake signal over database-backed jobs.
- `JobProcessor` (hosted service) claims queued jobs, processes with concurrency, recovers stale running jobs, and marks terminal states.
- Cancellation calls `JobProcessor.Cancel`, sets state to `Cancelled`, and records events.
- Job APIs enforce ownership; admin sees all jobs.

## Views and APIs

- Iterator Views are MVC Razor; API endpoints are minimal APIs grouped under `/api`.
- Runtime status DTO masks secrets and snapshots.

## Data Protection and Secrets

- Secret protection protects PAT and tracker credentials at rest.
- Data Protection persists key ring when configured.
- Browser and cookie settings are hardened for production.

## Testing

- `tests/Symphony.Tests` is the CI suite.
- Integration and Smoke tests are excluded by default.
- Playwright is run through the MCP against a launched host.
- CI collects Cobertura coverage and fails if no coverage artifact is produced.