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
| `SYMPHONY_WORKSPACE_ROOT` | all | no | Root directory for job workspaces. The image sets `/home/app/workspaces`; compose mounts the `workspaces` volume there. |
| `LINEAR_API_KEY` | all | no | Linear API token. Used by the Linear tracker and to authenticate the Linear MCP server for agent jobs. Required when a job submits Linear tickets. |
| `ASPNETCORE_ENVIRONMENT` | all | no | Use `Production` in containers. |
| `ASPNETCORE_URLS` | all | no | Image sets `http://+:8080`. |

## Data Protection and persistence

Volumes used by the sample stack:

- `pgdata` -> PostgreSQL data (`/var/lib/postgresql/data`).
- `dataprotection` -> ASP.NET Core Data Protection keys (`/home/app/.aspnet/DataProtection-Keys`).
- `workspaces` -> job workspaces and repository checkouts (`/home/app/workspaces`).

Losing the `dataprotection` volume invalidates cookie-based sign-in and requires users to sign in again. Losing the `workspaces` volume only discards checkouts and per-user agent state; new jobs clone again.

When you use SQLite instead of PostgreSQL, mount a volume at `/app/App_Data` so the database and the default key ring survive container replacement. The root `docker-compose.yml` does this with the `appdata` volume.

Each Symphony user gets an isolated Copilot home under `/home/app/workspaces/<user>/.copilot`. The image does not use the shared `~/.copilot` because the container runs as a single OS user.

## Non-root runtime

The image runs as the non-root `app` user, UID/GID `1654`, matching the `APP_UID=1654` value in the .NET base images. `src/Symphony/Dockerfile` creates these paths with that ownership before switching users:

- `/app/App_Data` -> SQLite database and the default Data Protection key ring.
- `/home/app/.aspnet/DataProtection-Keys` -> key ring used by the sample stack.
- `/home/app/workspaces` -> job workspaces and per-user agent state.

Named volumes take their ownership from the image the first time Docker initializes them, so the sample stack works unchanged. Bind mounts do not: make them writable by UID `1654` first (`sudo chown -R 1654:1654 <host-directory>`), or startup fails with `UnauthorizedAccessException` on `/app/App_Data`.

Do not run the container with `-u 0`; root is neither required nor supported.

## Image build path

`src/Symphony/Dockerfile` is the supported image. The publish workflows (`.github/workflows/docker.yml`, `cd.yml`), both compose files, and the published `ghcr.io` tags build from it, and it installs the agent toolchain and creates the writable directories above.

`dotnet publish /t:PublishContainer` builds a different, SDK-generated image with no writable data directory and no agent toolchain. Running that image as a non-root user fails on `/app/App_Data`. Build from the Dockerfile instead.

## Linear in containers

The Linear MCP server is reached over native Streamable HTTP at `https://mcp.linear.app/mcp` with an `Authorization: Bearer` header built from the Linear credential. There is no browser OAuth step and no shared `~/.mcp-auth` directory, so it works headlessly.

Provide the credential as `LINEAR_API_KEY` (or as `tracker.api_key`, using `$LINEAR_API_KEY`). The sample stack forwards `LINEAR_API_KEY` into the app container, so setting it in `samples/.env` or the shell that runs `docker compose` is enough. A job that submits Linear tickets without a credential fails immediately with `linear_credentials_missing`. Jobs submitted as work items only do not need Linear and do not get the Linear MCP server attached.

## Git tooling in the image

The runtime stage installs what a coding agent needs to work on a repository: `git`, `gh`, `node`/`npm` (available for any stdio MCP servers launched with `npx`), `openssh-client`, `curl`, `jq`, and `unzip`. Linear itself uses native HTTP MCP and does not need `npx`. The image does not include the .NET SDK.

Workspace creation clones the submitted repository automatically (shallow, single branch), so the default workflow no longer needs a clone hook. Commit identity and the repository token are provided through the job environment, and the token reaches git as an `http.extraheader` value rather than being written to `.git/config`.


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
