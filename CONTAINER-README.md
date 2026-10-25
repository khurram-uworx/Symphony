# Running Symphony in a container

Symphony is published as a container image:

- `ghcr.io/khurram-uworx/symphony:latest`
- `ghcr.io/khurram-uworx/symphony:<version>` (from release tags)

The sample stack in `samples/docker-compose.yml` uses the published image with PostgreSQL.

## Quick start

```bash
docker compose -f samples/docker-compose.yml up
```

Compose reads `samples/.env` automatically. The first run pulls the image and PostgreSQL, initializes the schema, and seeds the first admin.

Watch the app logs for the generated administrator password:

```bash
docker compose -f samples/docker-compose.yml logs -f app
```

Look for:

```text
Symphony admin credentials (shown once)
Username: testadmin
Password: <generated>
```

When `SeedAdmin__Password` is set, the password line becomes `Password: (configured via SeedAdmin:Password)` and the value is never written to the logs.

Then open `http://localhost:8080`, sign in, and you should see `Hello, <user>`.

## Sample environment

`samples/.env` contains sample-only defaults. Change every value before using this outside local evaluation. In particular, change `POSTGRES_PASSWORD` and the matching `ConnectionStrings__PostgreSQL` password.

## Environment variables

| Variable | Provider | Required | Notes |
| --- | --- | --- | --- |
| `Database__Provider` | all | no | `sqlite` (default), `sqlserver`, `postgresql`. Aliases: `postgres`, `npgsql`. |
| `ConnectionStrings__Sqlite` | sqlite | no | Defaults to `Data Source=App_Data/symphony.db`. |
| `ConnectionStrings__SqlServer` | sqlserver | yes | No default. |
| `ConnectionStrings__PostgreSQL` | postgresql | yes | Preferred key. |
| `ConnectionStrings__Npgsql` | postgresql | fallback | Used only when `PostgreSQL` is not set. |
| `SeedAdmin__UserName` | all | no | First admin username. Defaults to `testadmin`. |
| `SeedAdmin__Password` | all | no | First admin password. Empty generates and prints one once; a configured value is used verbatim without policy validation and is never printed. |
| `ApiKeys__ServerKey` | all | yes in Production for API keys | Server key for API key HMAC hashing. Required in Production; a development default is used otherwise. |
| `DataProtection__KeysPath` | all | no | Directory for Data Protection key ring. Defaults to `App_Data/DataProtection-Keys` under the content root. |
| `ASPNETCORE_ENVIRONMENT` | all | no | Use `Production` in containers. |
| `ASPNETCORE_URLS` | all | no | Image sets `http://+:8080`. |

## Data Protection and persistence

Volumes used by the sample stack:

- `pgdata` -> PostgreSQL data (`/var/lib/postgresql/data`).
- `dataprotection` -> ASP.NET Core Data Protection keys (`/home/app/.aspnet/DataProtection-Keys`).

Losing the `dataprotection` volume invalidates cookie-based sign-in and requires users to sign in again.

The image runs as the `app` user and already creates `/home/app/.aspnet/DataProtection-Keys`.

## Schema initialization

No manual SQL is required. Startup runs EF Core migrations for the selected provider:

- `sqlite` -> `Symphony.Migrations.Sqlite`
- `sqlserver` -> `Symphony.Migrations.SqlServer`
- `postgresql` / `postgres` / `npgsql` -> `Symphony.Migrations.PostgreSQL`

Migration SQL is provider-specific and is not portable between providers.

Databases created by the older `EnsureCreated` path have no `__EFMigrationsHistory` and cannot be adopted. Drop them before starting with migrations.

SQLite creates its parent directory automatically before migrations run.

## Secret handling

- `samples/.env` is sample-only. Do not commit production secrets.
- Prefer injecting real secrets through your orchestrator or secret store.
- The first-admin password is generated once and printed to container stdout. Capture it from the logs immediately.
