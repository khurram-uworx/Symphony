# T7: Schema initialization and seed data (#12)

Branch: `khurram/12`

Human confirmed this branch, authorized `dotnet test` for this work, and authorized push plus a pull request when the plan is done. After the PR exists, test the branch with the Playwright MCP, fix issues found, then update the PR description and add a concluding comment on what was tested and how.

As each task executes, if deferred work or a concern appears outside this plan, create a GitHub issue immediately (`gh issue create --repo khurram-uworx/Symphony`) and record its number in the GitHub issues log. Do not hold it in memory.

## Problem

Issue #12 requires startup to create a usable schema for the configured provider without manual SQL, then invoke seeds idempotently.

Today `DatabaseInitializer.InitializeAsync` always calls `EnsureCreatedAsync`. There are no EF Core migrations. Microsoft docs say `EnsureCreated` and migrations must not be mixed: a database created that way has no `__EFMigrationsHistory` and cannot later be migrated.

Already done, and must not be redone or duplicated:

- SQLite parent directory creation in `SqliteConnectionResolver.Resolve`, called from `DatabaseRegistration.AddSymphonyDatabase`.
- Roles and first-admin seeding in `AdminSeeder.SeedAsync` (T3). `Program.cs` already calls it after schema init. Restart does not duplicate the admin or print credentials again.

Not in this task:

- Canonical `AppSetting` values and bundled workflow seeding. Those belong to T18 (# issue not yet filed as a separate execution task; the product plan owns them). Do not add an `AppSetting` entity or placeholder keys.
- Container files and the container guide (T8 / T8D).

## Decisions already made

- Always `MigrateAsync`. No `EnsureCreated` on the startup path. No Development/Production split.
- Existing local databases may be thrown away. `App_Data/` is gitignored. Document that an old `EnsureCreated` file must be deleted.
- One `SymphonyDbContext`. No derived provider contexts.
- One migration set is not possible. EF Core scaffolds provider-specific store types (`TEXT`, `nvarchar(450)`, `character varying`). [Migrations with Multiple Providers](https://learn.microsoft.com/ef/core/managing-schemas/migrations/providers) requires a separate assembly per provider when there is one context type. Issue #12 says the same.
- SQL Server gets a migration set too. Registration already supports it. `MigrateAsync` with no migrations would create an empty database and no Identity tables, which regresses today's `EnsureCreated` behavior.
- The context moves to `src/Symphony.Data` so migration projects can reference it without a cycle. The web app references the migration projects so runtime `MigrateAsync` can load them.
- Defer `AppSetting` and workflow content to T18. Keep calling `AdminSeeder` after schema init. Do not add a seeder abstraction.

## Proposed changes

### 1. Data class library

`src/Symphony.Data/Symphony.Data.csproj` (`net10.0`, nullable, implicit usings).

Move only `SymphonyDbContext` here. Namespace stays `Symphony.Data`.

Package references: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.13 and `Microsoft.EntityFrameworkCore.Relational` 10.0.13. No database provider on this project.

`src/Symphony/Symphony.csproj` references this project and drops the Identity EF package only if nothing left in the web app needs it. `AdminSeeder` and Identity registration stay in the web app and still need `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (or the shared framework plus the package). Keep the package on the web app if the move would otherwise fail to compile. Do not move `AdminSeeder`, `DatabaseInitializer`, `DatabaseRegistration`, `SqliteConnectionResolver`, or `IdentityRegistration`.

### 2. Three migration projects

- `src/Symphony.Migrations.Sqlite`
- `src/Symphony.Migrations.SqlServer`
- `src/Symphony.Migrations.PostgreSQL`

Each is a class library referencing `Symphony.Data`, its provider package (same versions as the web app: Sqlite/SqlServer 10.0.13, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3), and `Microsoft.EntityFrameworkCore.Design` 10.0.13 as a private asset.

Each has `IDesignTimeDbContextFactory<SymphonyDbContext>` with a dummy non-secret connection string and no extra Identity store options. Runtime `IdentityRegistration` does not set `Stores.SchemaVersion` or `MaxLengthForKeys`; factories must not either.

Add a repo-local `dotnet-ef` tool at 10.0.13 (`.config/dotnet-tools.json`).

Generate `InitialIdentity` with the migrations project as both `--project` and `--startup-project`, so `Program.cs` does not boot:

```bash
dotnet tool restore
dotnet ef migrations add InitialIdentity --project src/Symphony.Migrations.Sqlite --startup-project src/Symphony.Migrations.Sqlite
dotnet ef migrations add InitialIdentity --project src/Symphony.Migrations.SqlServer --startup-project src/Symphony.Migrations.SqlServer
dotnet ef migrations add InitialIdentity --project src/Symphony.Migrations.PostgreSQL --startup-project src/Symphony.Migrations.PostgreSQL
```

Do not hand-edit generated migration SQL except to remove a secret if one is emitted. None should be.

### 3. Registration and startup

In `DatabaseRegistration.AddSymphonyDatabase`, set the migrations assembly beside each provider:

```csharp
options.UseSqlite(resolved, o => o.MigrationsAssembly("Symphony.Migrations.Sqlite"));
options.UseSqlServer(sqlServer, o => o.MigrationsAssembly("Symphony.Migrations.SqlServer"));
options.UseNpgsql(postgres, o => o.MigrationsAssembly("Symphony.Migrations.PostgreSQL"));
```

`DatabaseInitializer.InitializeAsync` calls `db.Database.MigrateAsync(cancellationToken)` and nothing else.

`Program.cs` order stays: initialize, then `AdminSeeder.SeedAsync`.

Add the new projects to `Symphony.slnx`.

### 4. Tests

`tests/Symphony.Tests` gets direct project references to `Symphony.Data` and `Symphony.Migrations.Sqlite` so `MigrateAsync` can resolve the assembly under `dotnet test`.

- `DatabaseInitializerTests`: a new SQLite file gets Identity tables and exactly one `__EFMigrationsHistory` row. A second `InitializeAsync` does not add another row. Users and roles stay empty.
- `AdminSeederTests.createInitializedProvider` already goes through `AddSymphonyDatabase`. It should keep passing once the SQLite migrations assembly is referenced.
- `DatabaseRegistrationTests`: assert the configured migrations assembly name for sqlite, sqlserver, and npgsql without calling `InitializeAsync` and without opening SQL Server or PostgreSQL.

No Docker, no live PostgreSQL, no Playwright in `Symphony.Tests` (ADR-0002). Live PostgreSQL schema creation stays with T8 / T8C.

### 5. Developer docs

Replace the README sentence that says provider-specific migrations are not part of registration. State that startup applies the migrations assembly for `Database:Provider`, that no manual SQL is required, and that an old `EnsureCreated` file must be deleted.

Do not write `CONTAINER-README.md`. Record the T8D facts in the GitHub issues log as a note on existing issue #17 if a comment is useful, or leave them in this plan until G2. Prefer a short comment on #17 so the facts are not lost when `docs/TODO.md` is deleted:

- One migration assembly per provider, selected by `Database:Provider`. `postgresql`, `postgres`, and `npgsql` share the Npgsql set.
- Startup calls `MigrateAsync`. SQLite's parent directory is created first by `SqliteConnectionResolver`.
- Migration SQL is not portable.
- Databases created by the old `EnsureCreated` path must be dropped.

## Blast radius

Preliminary, to be confirmed at G1:

- `SymphonyDbContext` moves assemblies. Callers: `DatabaseInitializer`, `DatabaseRegistration`, `AdminSeeder`, `IdentityRegistration`, `Program.cs`, and the data tests. Namespace does not change, so call sites stay source-compatible if they reference the new assembly.
- `DatabaseInitializer.InitializeAsync` behavior changes from `EnsureCreatedAsync` to `MigrateAsync`. Callers: `Program.cs`, `DatabaseInitializerTests`, `DatabaseRegistrationTests.AddSymphonyDatabase_MissingDirectory_CreatesDatabaseOnInitialize`, `AdminSeederTests.createInitializedProvider`.
- `AddSymphonyDatabase` gains `MigrationsAssembly`. Covered by `DatabaseRegistrationTests`.
- Publishing `src/Symphony/Symphony.csproj` (future T8) will include the migration assemblies via `ProjectReference`. No Dockerfile exists yet.
- No change to cookie auth, controllers, or admin seeding rules.

## Verification

Ask is already granted for this task. Before each commit that changes code, `dotnet build Symphony.slnx`. Before the test commit and again before G2, run:

```bash
dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"
```

Do not run Integration tests.

After the PR exists, Playwright MCP against `dotnet run` on a disposable SQLite file: home page loads, startup does not throw, first-boot admin credentials appear once in stdout. A redirect from `/` to login is expected (ADR-0002) and is not a failure.

## Planned commits

1. `docs: plan T7 schema initialization in TODO.md`
2. `Move SymphonyDbContext into a data class library`
3. `Add provider-specific InitialIdentity migrations`
4. `Apply provider migrations on startup`
5. `Test schema initialization and migrations assembly selection`
6. `Document startup migrations and the EnsureCreated break`
7. Fix commits only if review or Playwright finds a gap.
8. `docs: remove TODO.md — plan executed` after G2 clears.

## GitHub issues log

- [x] #17 — T8D must record provider-specific migration differences (commented on the issue during the docs step)
