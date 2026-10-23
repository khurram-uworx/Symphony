# Symphony .NET

A .NET 10 web host for AI agent orchestration.

> [!WARNING]
> Symphony is prototype software intended for evaluation and development purposes
> For production deployments, implement your own hardened version

## Overview

Symphony runs as an ASP.NET Core MVC web host. Start it with `dotnet run --project src/Symphony` and open the site. A workflow file is not required to start.

The web host uses ASP.NET Core Identity for sign-in, selects its database through configuration, applies EF Core migrations on startup, and seeds a first administrator whose generated password is printed to the console. A greeting is shown after sign-in.

## Getting Started

### Prerequisites

- .NET 10 SDK installed

### Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/khurram-uworx/symphony
   cd symphony
   ```

2. Build the project:
   ```bash
   dotnet build Symphony.slnx
   ```

### Running Symphony

Start the web host. No workflow path is required:

```bash
dotnet run --project src/Symphony
```

Kestrel listens on `http://localhost:5080`. Open that address in a browser and sign in with the seeded administrator credentials printed to the console.

### Running the published container image

A release image is published to `ghcr.io/khurram-uworx/symphony`. See [CONTAINER-README.md](CONTAINER-README.md) and `samples/docker-compose.yml` for a ready-to-run PostgreSQL stack.

## Configuration

### Web host database

The web host selects its database from `appsettings.json`, overridden by environment variables. SQLite is the default and does not need a server. Switching providers is configuration only; do not change code.

| `Database:Provider` | Connection string | Notes |
| --- | --- | --- |
| `sqlite` (default) | `ConnectionStrings:Sqlite` | Defaults to `Data Source=App_Data/symphony.db` when omitted |
| `sqlserver` | `ConnectionStrings:SqlServer` | Required. No default |
| `postgresql` | `ConnectionStrings:PostgreSQL` | Required. `postgres` and `npgsql` are provider aliases. `ConnectionStrings:Npgsql` is used only when `PostgreSQL` is not set |

Environment variables use `__` for nested keys: `Database__Provider`, `ConnectionStrings__SqlServer`, `ConnectionStrings__PostgreSQL`.

A missing SQL Server or PostgreSQL connection string fails at startup. The error names the missing key and the configured provider.

Startup applies the EF Core migrations assembly for the selected provider. No manual SQL is required. `sqlite` uses `Symphony.Migrations.Sqlite`, `sqlserver` uses `Symphony.Migrations.SqlServer`, and `postgresql` / `postgres` / `npgsql` share `Symphony.Migrations.PostgreSQL`. Those sets are not portable: each one hardcodes that provider's store types. For SQLite, the parent directory is created before migrations run.

Delete a database created by the old `EnsureCreated` path before starting again. That file has no `__EFMigrationsHistory`, and migrations cannot adopt it. `App_Data/` is gitignored.

Add the next model change to every provider set. Use the migrations project as both project and startup project so the web host does not boot, migrate, or print admin credentials:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Symphony.Migrations.Sqlite --startup-project src/Symphony.Migrations.Sqlite
dotnet ef migrations add <Name> --project src/Symphony.Migrations.SqlServer --startup-project src/Symphony.Migrations.SqlServer
dotnet ef migrations add <Name> --project src/Symphony.Migrations.PostgreSQL --startup-project src/Symphony.Migrations.PostgreSQL
```

Legacy tracker workflow configuration is documented in [docs/CONFIG.md](docs/CONFIG.md). It is not required to run the web host.

## Project Structure

```text
src/Symphony/                    # Web host
  Program.cs                    # ASP.NET Core entry point
  Controllers/                  # MVC controllers
  Views/                        # MVC views
  Agent/                        # Copilot SDK and Codex runners
  Config/
  Domain/
  IssueTracker/                 # Linear and Jira
  Orchestration/                # Orchestration components
  Workflows/
  Workspaces/
  Utils/
tests/
  Symphony.Tests/               # NUnit suite (CI)
  Symphony.CodingAgentsProbe/   # Manual Copilot SDK probes (not CI)
  Symphony.PerformanceTests/    # Manual performance harness (not CI)
docs/
  CONFIG.md
  adr/                          # Accepted architecture decisions
  plans/                        # Product and implementation plans
```

## Environment Variables

- **LINEAR_API_KEY**: Linear or Jira personal API token (used by the legacy tracker clients; not required to start the web host)
- **SYMPHONY_WORKSPACE_ROOT**: Override default workspace root directory
- **SeedAdmin__UserName**: First administrator username (default `testadmin`)

## Development

### Building

```bash
dotnet build Symphony.slnx
```

### Running Tests

The NUnit suite is the only project intended for CI:

```bash
dotnet test tests/Symphony.Tests
```

CI (`.github/workflows/ci.yml`) builds and tests only `tests/Symphony.Tests` with the filter `Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness` and collects Cobertura coverage via `coverlet.collector`. The job fails if tests run but no coverage file is produced.

`tests/Symphony.CodingAgentsProbe` and `tests/Symphony.PerformanceTests` are manual. Do not treat them as part of CI.

```bash
dotnet run --project tests/Symphony.CodingAgentsProbe -- copilot "Say hello in one sentence."
dotnet run -c Release --project tests/Symphony.PerformanceTests
```

### Container image

A release image is published to `ghcr.io/<owner>/symphony` (tagged with the version and `latest`) by `.github/workflows/cd.yml` on a `v*` tag or via manual dispatch. `.github/workflows/docker.yml` can publish a requested tag manually.

Architecture decisions live in [docs/adr/](docs/adr/). Product plans live in [docs/plans/](docs/plans/).

## How It Works

1. **Start**: Kestrel serves the MVC application on the configured URL.
2. **Database**: `DatabaseInitializer` applies the provider-specific EF Core migrations.
3. **Seed**: `AdminSeeder` creates the Admin and User roles and the first administrator if none exists, printing the generated password once.
4. **Sign in**: Users authenticate through `/Account/Login`; successful login lands on the home page, which greets the signed-in user.
5. **Data Protection**: keys persist under `/home/app/.aspnet/DataProtection-Keys` in containers so sign-in survives restarts.

## Contributing

Contributions are welcome! Please ensure:
- Code builds without warnings
- Tests pass
- Code follows the existing style conventions

## License

This project is licensed under the [Apache License 2.0](LICENSE)
