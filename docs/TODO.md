# Plan: T2 Identity and SQLite bootstrap (#7)

Issue: https://github.com/khurram-uworx/Symphony/issues/7
Branch: `khurram/7`

As each task executes, if deferred work or a concern appears that is outside this plan, create a GitHub issue immediately (`gh issue create --repo khurram-uworx/Symphony`) and record its number in the GitHub issues log below. Do not rely on memory or wait until the plan finishes.

## Problem

The web host starts without a database or authentication. Multi-user Identity needs a `SymphonyDbContext`, SQLite schema creation, and cookie authentication wired before T3 can seed an admin. There is no `Data/` folder, no EF packages, and `appsettings.json` has no `Database` section.

## Decisions already cleared

- Schema initialization is `EnsureCreated` only. No migration files and no `dotnet-ef` tool. `Data/DatabaseInitializer` is the only call site so T7 (#12) can replace it. A database created by `EnsureCreated` cannot be migrated in place; delete `App_Data/symphony.db` when T7 switches.
- Tests are unit tests against a temp file. No `WebApplicationFactory`, no public `partial Program`, no `Microsoft.AspNetCore.Mvc.Testing`.
- SQLite is the only fully supported provider. `sqlserver` and `postgresql` / `postgres` / `npgsql` throw a clear "not supported in this build" error at registration so T6 (#11) replaces those arms. Do not add SQL Server or Npgsql packages.
- Stock `IdentityUser` / `IdentityRole`. No custom user, no role or admin seeding (T3, #8), no `AppSetting` or workflow seed (T7/T18).
- `AddIdentity`, not `AddDefaultIdentity`. No Identity UI, no `MapRazorPages()`.
- Cookie paths: `/Account/Login`, `/Account/Logout`, `/Account/AccessDenied`. Leave Identity password defaults unchanged. Do not set `RequireConfirmedAccount`. `/` stays anonymous.
- Pin `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, and `Microsoft.EntityFrameworkCore.Design` to `10.0.13`. Design package uses `PrivateAssets=all`.

## Blast radius

Startup of `src/Symphony` now opens SQLite and calls `EnsureCreated` before `Run`. A bad `Database:Provider` or an unwritable data directory fails startup instead of serving `/`. Anonymous `GET /` must still return the existing landing view.

- `src/Symphony/Symphony.csproj` — three package references. Design is private so it does not flow to tests.
- `src/Symphony/Data/SymphonyDbContext.cs` — new. No callers yet. T3+ will inject it.
- `src/Symphony/Data/SqliteConnectionResolver.cs` — new. Called from registration.
- `src/Symphony/Data/DatabaseRegistration.cs` — new extension `AddSymphonyDatabase`. Called from `Program.cs`.
- `src/Symphony/Data/IdentityRegistration.cs` — new extension `AddSymphonyIdentity`. Called from `Program.cs`.
- `src/Symphony/Data/DatabaseInitializer.cs` — new. Called once from `Program.cs` after `Build()`.
- `src/Symphony/Program.cs` — composition root. Top-level statements, not a symbol in the index. Adds auth middleware before the existing conventional route. No global authorize filter.
- `src/Symphony/appsettings.json` — `Database:Provider` and `ConnectionStrings:Sqlite`.
- `.gitignore` — `App_Data/`.
- `tests/Symphony.Tests/` — new unit tests. Existing `DapploJiraTests` is an integration fixture and must stay green; it does not boot the host.
- Not touched: agents, orchestration, issue trackers, workflows, views, `README.md`, `AGENTS.md`, `opencode.json`, Docker (T8, #13).

`HomeController.Index` has no dependencies. Auth middleware does not challenge anonymous MVC requests, so `/` behavior is unchanged unless startup throws.

## Proposed changes

### 1. Packages

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.13" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.13" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.13">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

### 2. `SymphonyDbContext`

Sealed, primary constructor, `IdentityDbContext<IdentityUser, IdentityRole, string>`. No extra `DbSet`s.

### 3. SQLite path resolution

`SqliteConnectionResolver.Resolve(connectionString, contentRoot)`:

- Parse with `SqliteConnectionStringBuilder`.
- Leave `:memory:` unchanged and do not create a directory.
- If `DataSource` is relative, combine with the content root and `Path.GetFullPath`.
- If it is already rooted, keep it.
- `Directory.CreateDirectory` on the parent when there is one.
- Return `builder.ToString()` so other keys (for example `Cache=Shared`) survive.

### 4. Registration

`AddSymphonyDatabase(IConfiguration, contentRoot)`:

- Read `Database:Provider`, default `sqlite` only when the key is absent.
- Normalize with `ToLowerInvariant()`. Treat `postgresql`, `postgres`, and `npgsql` as `npgsql`.
- `sqlite`: connection string from `ConnectionStrings:Sqlite`, default `Data Source=App_Data/symphony.db`. Resolve the path, then `AddDbContextFactory<SymphonyDbContext>(o => o.UseSqlite(resolved))`.
- Also `AddScoped(sp => sp.GetRequiredService<IDbContextFactory<SymphonyDbContext>>().CreateDbContext())` so `AddEntityFrameworkStores` can construct `UserStore`. The container disposes that scoped instance.
- `sqlserver` and `npgsql`: throw `InvalidOperationException` that this build supports sqlite only.
- Anything else: throw `Unsupported database provider`.

`AddSymphonyIdentity()`:

```csharp
services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<SymphonyDbContext>()
    .AddDefaultTokenProviders();
services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
```

`ConfigureApplicationCookie` must run after `AddIdentity`.

### 5. Startup

In `Program.cs`, after `AddControllersWithViews()`:

```csharp
builder.Services.AddSymphonyDatabase(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSymphonyIdentity();
```

After `Build()`, before `MapControllerRoute`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

After the route map and before `Run()`:

```csharp
await DatabaseInitializer.InitializeAsync(
    app.Services.GetRequiredService<IDbContextFactory<SymphonyDbContext>>());
```

`DatabaseInitializer` creates a context from the factory and calls `EnsureCreatedAsync`. No seeding.

### 6. Config and ignore

`appsettings.json`:

```json
"Database": { "Provider": "sqlite" },
"ConnectionStrings": { "Sqlite": "Data Source=App_Data/symphony.db" }
```

No `SeedAdmin`. No Development override. Gitignore `App_Data/`.

### 7. Tests

NUnit, `Assert.That`, no `[TestCase]`, names `Method_Scenario_ExpectedBehavior`. One test class per source class. Temp directories under the test run, deleted after the context is disposed.

- Relative `Data Source=App_Data/symphony.db` becomes a full path under the supplied content root, and the missing parent directory is created.
- An already absolute `Data Source` is not prefixed with the content root; its parent is still created when missing.
- `:memory:` is unchanged and does not create a directory.
- `DatabaseInitializer.InitializeAsync` on a temp file creates `AspNetUsers` and `AspNetRoles` (query succeeds, count is zero).
- `AddSymphonyDatabase` throws for `sqlserver` and for an unknown provider, and does not throw for `sqlite`.

## Verification

- `dotnet build Symphony.slnx` after each code commit.
- Ask before `dotnet test`. When confirmed, `dotnet test tests/Symphony.Tests`.
- Do not start the host as part of the unit tests.

## PR test plan

After the PR exists, rebuild rather than reuse a stale image if a container test is run. T8 (#13) owns the Dockerfile; this issue does not add one. If no Symphony image exists, run the host with `dotnet run --project src/Symphony` on `http://localhost:5080` after deleting `src/Symphony/App_Data`, then:

- `GET /` returns the existing landing view (`Symphony` / `The web host is running.`).
- `src/Symphony/App_Data/symphony.db` exists.
- Identity tables exist in that file.
- Playwright against `/` confirms the page renders.

## Planned commits

1. `docs: plan issue 7 Identity SQLite bootstrap in TODO.md`
2. `feat: add SymphonyDbContext and SQLite connection resolution`
3. `feat: bootstrap Identity and SQLite schema at startup`
4. `test: cover SQLite path resolution and Identity schema creation`
5. `docs: remove TODO.md — plan executed` after G2 clears

## GitHub issues log

- #7 — this plan. Do not create a duplicate.
- #11 — T6 replaces the non-sqlite throw arms. Do not implement those providers here.
- #12 — T7 replaces `EnsureCreated` and owns seed data. Do not add migrations here.
- #8 — T3 owns first-admin seeding. Do not seed users or roles here.
- #13 — T8 owns the Dockerfile. Do not add one here.
