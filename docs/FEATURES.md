# Features

Current, implemented capabilities of the Symphony .NET web host. For runtime/design details, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Web Host

- ASP.NET Core MVC app, entry point `src/Symphony`.
- Runs with `dotnet run --project src/Symphony`; no workflow file required.
- SQLite by default; SQL Server and PostgreSQL via configuration.
- Applies provider-specific EF Core migrations at startup.
- Data Protection key ring is persisted when configured.

## Identity and Administration

- ASP.NET Core Identity cookie authentication.
- Admin/user roles seeded on first start; first admin password printed once.
- Admin user management at `/admin/users`.
- Admin job management at `/admin/jobs` and `/admin/jobs/{id}` with owner/status/date filters and pagination.
- Account area with configuration, workflows, and run launcher.

## Configuration

- Per-user `UserConfiguration` via `GET/PUT /api/configuration`.
- Secret fields are masked in API responses.
- Copilot BYOM provider validation (`openai`, `azure`, `anthropic`).

## Workflows

- User-owned named workflows via `/api/workflows`.
- Default workflow seeded per user.
- Workflow content is the master instruction with front matter overrides.

## Work Submission and Jobs

- `POST /api/work` accepts ticket IDs or work items and queues a job.
- Encrypted request snapshot; PAT never stored or returned in plaintext.
- `GET /api/jobs`, `GET /api/jobs/{id}`, `DELETE /api/jobs/{id}` with owner/admin rules.
- Background processor runs queued jobs, recovers stale jobs, handles cancellation and retry link (`RetryOfJobId`).
- Job status DTO includes issue progress, errors, and summary.

## GitHub Integration

- GitHub Issues can be used as the work source (`tracker.kind: github`).
- GitHub repositories are cloned and synchronized per job using per-user Personal Access Tokens.
- Secrets are encrypted and never returned in plaintext.
- See [FEATURES-GITHUB.md](FEATURES-GITHUB.md) for details.

## Trackers and Agents

- Linear, Jira, and GitHub Issues trackers (optional context sources).
- Copilot SDK and Codex coding agents.
- Workspace management and hooks.
- Null tracker for inline work items.

## Documentation and References

- `docs/CONFIG.md` - legacy tracker workflow configuration.
- `docs/adr/` - accepted architecture decisions.
- `docs/plans/` - product and implementation plans.
- `docs/FEATURES-GITHUB.md` - GitHub integration details.

## Out Of Scope In CI

- `tests/Symphony.CodingAgentsProbe` and `tests/Symphony.PerformanceTests` are manual.
- Playwright/browser checks run via the Playwright MCP against a launched host.