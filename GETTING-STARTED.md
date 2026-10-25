# Getting Started

## Prerequisites

- .NET 10 SDK installed

## Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/khurram-uworx/symphony
   cd symphony
   ```

2. Build the project:
   ```bash
   dotnet build Symphony.slnx
   ```

## Running Symphony

Start the web host. No workflow path is required:

```bash
dotnet run --project src/Symphony
```

Kestrel listens on `http://localhost:5080`. Open that address in a browser and sign in with the seeded administrator credentials printed to the console.

## Running the published container image

A release image is published to `ghcr.io/khurram-uworx/symphony`. See [CONTAINER-README.md](CONTAINER-README.md) and `samples/docker-compose.yml` for a ready-to-run PostgreSQL stack.

## Environment Variables

| Variable | Purpose | Default |
| --- | --- | --- |
| `LINEAR_API_KEY` | Linear or Jira personal API token (used by legacy tracker clients; not required to start the web host) | - |
| `SYMPHONY_WORKSPACE_ROOT` | Override default workspace root directory | - |
| `SeedAdmin__UserName` | First administrator username | `testadmin` |
| `SeedAdmin__Password` | First administrator password. Empty generates a strong password and prints it once; a configured value is used verbatim, is not policy-validated, and is never printed. | (generated) |
| `Database__Provider` | Database provider: `sqlite`, `sqlserver`, `postgresql` (aliases: `postgres`, `npgsql`) | `sqlite` |
| `ConnectionStrings__Sqlite` | SQLite connection string | `Data Source=App_Data/symphony.db` |
| `ConnectionStrings__SqlServer` | SQL Server connection string (required if provider is sqlserver) | - |
| `ConnectionStrings__PostgreSQL` / `ConnectionStrings__Npgsql` | PostgreSQL connection string (required if provider is postgresql) | - |
| `ApiKeys__ServerKey` | Server key for API key HMAC hashing. Required in Production for multi-instance deployments. | dev-only default outside Production |

Environment variables use `__` for nested keys.

## Configuration

### Web host database

The web host selects its database from `appsettings.json`, overridden by environment variables. SQLite is the default and does not need a server. Switching providers is configuration only; do not change code.

| `Database:Provider` | Connection string | Notes |
| --- | --- | --- |
| `sqlite` (default) | `ConnectionStrings:Sqlite` | Defaults to `Data Source=App_Data/symphony.db` when omitted |
| `sqlserver` | `ConnectionStrings:SqlServer` | Required. No default |
| `postgresql` | `ConnectionStrings:PostgreSQL` | Required. `postgres` and `npgsql` are provider aliases. `ConnectionStrings:Npgsql` is used only when `PostgreSQL` is not set |

A missing SQL Server or PostgreSQL connection string fails at startup. The error names the missing key and the configured provider.

Startup applies the EF Core migrations assembly for the selected provider. No manual SQL is required. `sqlite` uses `Symphony.Migrations.Sqlite`, `sqlserver` uses `Symphony.Migrations.SqlServer`, and `postgresql` / `postgres` / `npgsql` share `Symphony.Migrations.PostgreSQL`. Those sets are not portable: each one hardcodes that provider's store types. For SQLite, the parent directory is created before migrations run.

Delete a database created by the old `EnsureCreated` path before starting again. That file has no `__EFMigrationsHistory`, and migrations cannot adopt it. `App_Data/` is gitignored.

### Migrations

Add the next model change to every provider set. Use the migrations project as both project and startup project so the web host does not boot, migrate, or print admin credentials:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Symphony.Migrations.Sqlite --startup-project src/Symphony.Migrations.Sqlite
dotnet ef migrations add <Name> --project src/Symphony.Migrations.SqlServer --startup-project src/Symphony.Migrations.SqlServer
dotnet ef migrations add <Name> --project src/Symphony.Migrations.PostgreSQL --startup-project src/Symphony.Migrations.PostgreSQL
```

Legacy tracker workflow configuration is documented in [docs/CONFIG.md](docs/CONFIG.md). It is not required to run the web host.

## Running Tests

The NUnit suite is the only project intended for CI:

```bash
dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"
```

CI (`.github/workflows/ci.yml`) builds and tests only `tests/Symphony.Tests` with the filter `Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness` and collects Cobertura coverage via `coverlet.collector`. The job fails if tests run but no coverage file is produced.

`tests/Symphony.CodingAgentsProbe` and `tests/Symphony.PerformanceTests` are manual. Do not treat them as part of CI.

```bash
dotnet run --project tests/Symphony.CodingAgentsProbe -- copilot "Say hello in one sentence."
dotnet run -c Release --project tests/Symphony.PerformanceTests
```

## Submitting Work With curl (API Keys)

Symphony supports terminal-first workflows through user-owned API keys.

1. Sign in at `/Account/Login` (any role) and open `/Account/ApiKeys`.
2. Create an API key. The raw key — `Authorization: Bearer <key>` material — is displayed exactly once; only a prefix and an HMAC-SHA256 hash are stored.
3. Submit work from any terminal:

```bash
curl -X POST "http://localhost:5080/api/work" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <YOUR_API_KEY>" \
  --data '{
    "externalId": null,
    "ticketIds": ["ENG-1"],
    "workItems": [],
    "github": { "repository": "owner/repo", "personalAccessToken": "<YOUR_GITHUB_TOKEN>" },
    "workflowName": "<WORKFLOW_NAME>",
    "overrides": {}
  }'
```

The response returns `202 Accepted` with the job id and a status URL (`/api/jobs/{jobId}`); poll the status URL until the job reaches a terminal state (`Completed`, `CompletedWithFailures`, `Failed`, or `Cancelled`). The job and its progress are also visible in the web UI under `/Account/Jobs`. The `/Account/Run` page shows the same request as a copyable example and links to key management.

Key behavior:

- Keys are per-user: endpoints and work submitted with a key are attributed to the key owner. Admins cannot create, rotate, or revoke keys for other users.
- Keys support enable/disable (temporary), rotate (old key stopped, new one issued), and revoke (permanent).
- Disabling or deleting a user immediately invalidates all of their keys, and a disabled user cannot sign in.
- API requests are rate limited per user (120 requests per minute; `429 Too Many Requests` beyond that).
- The `X-API-Key` header is accepted as an alternative to `Authorization: Bearer`.

Key material is hashed with HMAC-SHA256 using a server key read from `ApiKeys:ServerKey` (a development-only default is used outside Production; the host refuses to start in Production without it). Set `ApiKeys__ServerKey` explicitly when running multiple instances so hashes stay comparable across nodes.
