# Symphony .NET

A .NET 10 web host for AI agent orchestration.

> [!WARNING]
> Symphony is prototype software intended for evaluation and development purposes. For production deployments, implement your own hardened version.

## Overview

Symphony runs as an ASP.NET Core MVC web host. Start it with `dotnet run --project src/Symphony` and open the site. A workflow file is not required to start.

The web host uses ASP.NET Core Identity for sign-in, selects its database through configuration, applies EF Core migrations on startup, and seeds a first administrator whose generated password is printed to the console. A greeting is shown after sign-in.

## Documentation

- [GETTING-STARTED.md](GETTING-STARTED.md) - setup, running, configuration, tests, and API key usage
- [CONTRIBUTING.md](CONTRIBUTING.md) - development guidelines and contribution process
- [ARCHITECTURE.md](docs/ARCHITECTURE.md) - design, runtime shape, and folder structure
- [FEATURES.md](docs/FEATURES.md) - what is available
- [CONFIG.md](docs/CONFIG.md) - legacy tracker workflow configuration
- [ADR index](docs/adr/) - accepted architecture decisions
- [Plans](docs/plans/) - product and implementation plans
- [AGENTS.md](AGENTS.md) - repository working rules
- [CONTAINER-README.md](CONTAINER-README.md) - container usage

## Quick Start

### Run the published container image (no .NET SDK needed)

```bash
git clone https://github.com/khurram-uworx/symphony
cd symphony
docker compose -f samples/docker-compose.yml up
```

Then open `http://localhost:8080`. The image is published at `ghcr.io/khurram-uworx/symphony:latest` (and version tags); see [CONTAINER-README.md](CONTAINER-README.md) for full container setup.

### Run with the .NET SDK

```bash
git clone https://github.com/khurram-uworx/symphony
cd symphony
dotnet build Symphony.slnx
dotnet run --project src/Symphony
```

See [GETTING-STARTED.md](GETTING-STARTED.md) for full details.

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

## How It Works

1. **Start**: Kestrel serves the MVC application on the configured URL.
2. **Database**: `DatabaseInitializer` applies the provider-specific EF Core migrations.
3. **Seed**: `AdminSeeder` creates the Admin and User roles and the first administrator if none exists, printing the generated password once.
4. **Sign in**: Users authenticate through `/Account/Login`; successful login lands on the home page, which greets the signed-in user.
5. **Data Protection**: keys persist under `/home/app/.aspnet/DataProtection-Keys` in containers so sign-in survives restarts.

## License

This project is licensed under the [Apache License 2.0](LICENSE.md)
