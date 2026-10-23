# T6: Multi-provider EF Core registration and configuration

GitHub issue: [#11](https://github.com/khurram-uworx/Symphony/issues/11)
Branch: `khurram/11`
Decision (human): leave `DatabaseInitializer.EnsureCreated` unchanged. T6 does not add provider-specific schema execution; that stays with T7.

Reminder: as each task executes, if deferred work or a concern appears outside this plan, create a GitHub issue immediately (`gh issue create --repo khurram-uworx/Symphony`) and record its number in the GitHub issues log. Do not hold it in memory.

## Problem

`DatabaseRegistration.AddSymphonyDatabase` registers `AddDbContextFactory<SymphonyDbContext>` for SQLite only. `sqlserver` and the PostgreSQL aliases throw `not supported in this build`. Issue #11 requires config-only switching among SQLite, SQL Server, and PostgreSQL, with clear errors when a required connection string is missing.

T2 already extracted registration out of `Program.cs`. T6 extends that registration. It does not replace it, and it does not put provider switching in `SymphonyDbContext`.

## Proposed changes

### Packages

Add to `src/Symphony/Symphony.csproj`, matching the existing EF Core 10.0.13 pin (not CodeMemory's older pins):

- `Microsoft.EntityFrameworkCore.SqlServer` `10.0.13`
- `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3` (depends on EF Core `>= 10.0.4 < 11.0.0`)

### Registration

Extend `Symphony.Data.DatabaseRegistration.AddSymphonyDatabase`. Keep the signature. Keep provider switching in this startup helper.

```csharp
static string normalize(string provider) =>
    provider.Trim().ToLowerInvariant() switch
    {
        "postgres" or "postgresql" or "npgsql" => "npgsql",
        var value => value
    };

static string require(IConfiguration configuration, string provider, params string[] keys)
{
    foreach (var key in keys)
    {
        var value = configuration.GetConnectionString(key);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
    }

    var names = string.Join("' or '", keys);
    throw new InvalidOperationException(
        $"Connection string '{names}' is required for Database:Provider '{provider}'.");
}

public static IServiceCollection AddSymphonyDatabase(...)
{
    var configured = configuration["Database:Provider"] ?? "sqlite";
    switch (normalize(configured))
    {
        case "sqlite":
            var sqlite = configuration.GetConnectionString("Sqlite");
            if (string.IsNullOrWhiteSpace(sqlite))
                sqlite = "Data Source=App_Data/symphony.db";
            var resolved = SqliteConnectionResolver.Resolve(sqlite, contentRoot);
            services.AddDbContextFactory<SymphonyDbContext>(options => options.UseSqlite(resolved));
            break;
        case "sqlserver":
            var sqlServer = require(configuration, configured, "SqlServer");
            services.AddDbContextFactory<SymphonyDbContext>(options => options.UseSqlServer(sqlServer));
            break;
        case "npgsql":
            var postgres = require(configuration, configured, "PostgreSQL", "Npgsql");
            services.AddDbContextFactory<SymphonyDbContext>(options => options.UseNpgsql(postgres));
            break;
        default:
            throw new InvalidOperationException($"Unsupported database provider '{configured}'.");
    }

    return services;
}
```

Rules:

- Missing or `sqlite` (any case) uses `ConnectionStrings:Sqlite`, defaulting to `Data Source=App_Data/symphony.db`. Relative paths and parent-directory creation stay in `SqliteConnectionResolver`, and only on this branch.
- `sqlserver` (any case) requires `ConnectionStrings:SqlServer`. No default.
- `postgres`, `postgresql`, and `npgsql` (any case) require `ConnectionStrings:PostgreSQL`, falling back to `ConnectionStrings:Npgsql`. `PostgreSQL` wins when both are set.
- Null or whitespace connection strings are missing. Do not rely on `??` alone.
- Errors name the missing key(s) and the provider value as configured.
- All three branches use the same `AddDbContextFactory<SymphonyDbContext>` overload so the scoped context and the factory both stay registered.
- Do not copy CodeMemory's per-repo `Func<DbContext>` factories, schema-per-repo helpers, or `PgVector` key.

### Left unchanged

- `Program.cs` — still calls `AddSymphonyDatabase`, then `DatabaseInitializer.InitializeAsync`.
- `SymphonyDbContext` — no `OnConfiguring` provider switch.
- `DatabaseInitializer` — `EnsureCreated` remains for whichever provider was registered.

### Config documentation

Default `appsettings.json` stays on SQLite with only `ConnectionStrings:Sqlite`. Do not add empty `SqlServer` or `PostgreSQL` values. Add comments (ASP.NET JSON allows them) documenting:

- `Database:Provider`: `sqlite` (default), `sqlserver`, `postgresql` (`postgres` and `npgsql` are aliases)
- keys: `Sqlite`, `SqlServer`, `PostgreSQL` (`Npgsql` is an alias)
- env vars: `Database__Provider`, `ConnectionStrings__SqlServer`, `ConnectionStrings__PostgreSQL`

Add a short web-host database section to `README.md`. Do not rewrite `docs/CONFIG.md`.

## Blast radius

- `Symphony.Data.DatabaseRegistration.AddSymphonyDatabase` — the change. It calls `normalize` and `SqliteConnectionResolver.Resolve`. Indexed callers are `DatabaseRegistrationTests`. `Program.cs` calls it from top-level statements (not in the symbol index). `AdminSeederTests.createInitializedProvider` also calls it with SQLite config.
- `SymphonyDbContext` — options only. No mapping change. Consumers (`IdentityRegistration`, `AdminSeeder`, `DatabaseInitializer`) keep resolving the same context.
- Tests that must stay green without edits: `AdminSeederTests`, `DatabaseInitializerTests`, `SqliteConnectionResolverTests`, `IdentityRegistrationTests`.
- No downstream schema, migration, or docker work.

## Verification

Ask before `dotnet test`. Planned checks:

1. `dotnet build Symphony.slnx`
2. `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
3. After the PR exists: launch with `dotnet run --project src/Symphony` and exercise the running app with Playwright MCP. Docker testing is out of scope.

Unit tests must not open a SQL Server or PostgreSQL connection. Creating a context and reading `Database.ProviderName` does not connect.

## Tests

Update `tests/Symphony.Tests/Data/DatabaseRegistrationTests.cs`. No `[TestCase]`. Replace the two "not supported in this build" tests.

- SQL Server with `ConnectionStrings:SqlServer` registers `Microsoft.EntityFrameworkCore.SqlServer`.
- `postgresql`, `postgres`, and `npgsql` each register `Npgsql.EntityFrameworkCore.PostgreSQL`.
- `PostgreSQL` wins over `Npgsql` when both are set.
- Missing SQL Server and missing PostgreSQL errors name the key and the configured provider.
- Unknown provider still throws.
- Two configurations through the same method yield different provider names.
- Existing SQLite factory, context, and directory tests stay.

## Planned commits

1. `docs: plan multi-provider EF registration in TODO.md`
2. `feat: register SQL Server and PostgreSQL EF providers`
3. `test: cover multi-provider database registration`
4. `docs: document production database connection settings`
5. `docs: remove TODO.md — plan executed` (only after G2 clears)

## GitHub issues log

- none yet
