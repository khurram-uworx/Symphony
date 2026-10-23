# T3: First-admin bootstrap and console credential output

GitHub issue: [#8](https://github.com/khurram-uworx/Symphony/issues/8)
Branch: `khurram/8`

As each task executes, if deferred work or a concern appears outside this plan, create a GitHub issue immediately (`gh issue create --repo khurram-uworx/Symphony`) and record its number in the GitHub issues log. Do not rely on memory or wait until the plan finishes.

## Problem

There is no public registration. The first admin must be seeded only when no `Admin` exists, with a cryptographically random password printed to the console exactly once. Restarts must not create another admin or print credentials. Raw passwords must not enter logs.

Today `Program.cs` calls `DatabaseInitializer.InitializeAsync`, which only runs `EnsureCreated`. `IdentityRegistration` registers `AddIdentity<IdentityUser, IdentityRole>()`. No seeder, no `SeedAdmin` config, and no credential output exist. `DatabaseInitializerTests` and `DatabaseRegistrationTests` expect zero users and zero roles after schema init, so seeding must stay a separate step.

## Decisions

- Seeder lives at `src/Symphony/Data/AdminSeeder.cs` in namespace `Symphony.Data` (not `src/Symphony/Identity/`).
- If no admin exists but `SeedAdmin:UserName` is already taken by a non-admin: fail startup. Do not reset the password. Do not print credentials.
- Call the seeder from `Program.cs` after schema init, not an `IHostedService`. Credentials print before the server listens.
- Seeded admin is in `Admin` only, not also `User`.
- Do not change password policy, add the Identity UI package, or seed workflows/settings.
- Do not register a second `SymphonyDbContext`. EF Core 10 `AddDbContextFactory` already registers the context as scoped. Dispose the scope, not the context.

## Proposed changes

### `src/Symphony/Data/AdminSeeder.cs`

Static class, matching `DatabaseInitializer`. No `ILogger`. Password is written only to the supplied `TextWriter` after commit.

```csharp
public static class AdminSeeder
{
    public const string AdminRole = "Admin";
    public const string UserRole = "User";
    public const string DefaultUserName = "testadmin";

    public static async Task SeedAsync(
        SymphonyDbContext db,
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        string? configuredUserName,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        var userName = string.IsNullOrWhiteSpace(configuredUserName)
            ? DefaultUserName
            : configuredUserName.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ensureRole(roles, AdminRole);
        await ensureRole(roles, UserRole);

        if ((await users.GetUsersInRoleAsync(AdminRole)).Count > 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (await users.FindByNameAsync(userName) is not null)
            throw new InvalidOperationException(
                $"Cannot seed admin '{userName}' because that username already exists and has no Admin role.");

        var password = generatePassword(users.Options.Password);
        var user = new IdentityUser { UserName = userName };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(error => error.Code == "DuplicateUserName")
                && (await users.GetUsersInRoleAsync(AdminRole)).Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            throw identityFailure(created);
        }

        var added = await users.AddToRoleAsync(user, AdminRole);
        if (!added.Succeeded)
            throw identityFailure(added);

        await transaction.CommitAsync(cancellationToken);
        await output.WriteLineAsync("Symphony admin credentials (shown once)");
        await output.WriteLineAsync($"Username: {userName}");
        await output.WriteLineAsync($"Password: {password}");
    }
}
```

Private helpers (`ensureRole`, `generatePassword`, `identityFailure`) are camelCase.

Password generation:

- `RandomNumberGenerator` only.
- Length `max(options.RequiredLength, 20)`.
- Satisfy `PasswordOptions` (defaults: digit, upper, lower, non-alphanumeric, min length 6). Do not weaken policy.
- Alphabet excludes whitespace, quotes, and backslash so the console line can be copied.
- Regenerate if `RequiredUniqueChars` is not met.

`UserManager` and `RoleManager` must be resolved from the same scope as `db` so `SaveChanges` enlists in that transaction. Print only after commit. On throw, the transaction disposes uncommitted and nothing is written.

Duplicate-username race: if `CreateAsync` returns `DuplicateUserName` and an admin now exists, roll back and return without printing. Otherwise fail.

### `src/Symphony/Program.cs`

After `DatabaseInitializer.InitializeAsync`:

```csharp
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    await AdminSeeder.SeedAsync(
        services.GetRequiredService<SymphonyDbContext>(),
        services.GetRequiredService<UserManager<IdentityUser>>(),
        services.GetRequiredService<RoleManager<IdentityRole>>(),
        app.Configuration["SeedAdmin:UserName"],
        Console.Out);
}
```

Do not log the password. `Console.Out` is not an `ILogger` sink.

### `src/Symphony/appsettings.json`

```json
"SeedAdmin": {
  "UserName": "testadmin"
}
```

Blank or whitespace config still falls back to `testadmin`.

### Unchanged

- `SymphonyDbContext` — Identity tables already come from `IdentityDbContext`.
- `DatabaseInitializer` — schema init must not seed.
- Password options and Identity UI.

## Tests

`tests/Symphony.Tests/Data/AdminSeederTests.cs`. Real SQLite file plus Identity services. `StringWriter` instead of the console. No mocks. NUnit `[Test]`, `Assert.That`, AAA, `Method_Scenario_ExpectedBehavior`.

- `SeedAsync_NoAdmin_CreatesRolesAdminAndWritesCredentials` — both roles, `testadmin` in `Admin` only, hash is not the raw password, `CheckPasswordAsync` succeeds, writer contains the pair once.
- `SeedAsync_SecondCall_PreservesAdminAndWritesNothing` — same user id and hash, writer empty.
- `SeedAsync_ExistingAdmin_DoesNotPrintOrOverwrite` — pre-existing admin with another name; no new user; writer empty.
- `SeedAsync_ConfiguredUserName_CreatesThatUser`.
- `SeedAsync_BlankUserName_UsesTestAdmin`.
- `SeedAsync_UserNameTakenByNonAdmin_ThrowsWithoutPrinting` — hash unchanged, writer empty.

Leave `DatabaseInitializerTests` and `DatabaseRegistrationTests` asserting zero users and roles after schema init.

## Verification

- `dotnet build Symphony.slnx`
- Ask before `dotnet test tests/Symphony.Tests`
- Confirm the seeder is not called from `DatabaseInitializer`

## Planned commits

1. `docs: plan first-admin bootstrap in TODO.md`
2. `feat: seed the first admin once and print credentials` — seeder plus tests
3. `feat: run admin seed at startup` — `Program.cs` and `appsettings.json`

## Blast radius

- New: `AdminSeeder`, `AdminSeederTests`. No existing callers.
- `Program.cs` startup order: schema init, then seed, then `app.Run()`. Failure to seed fails startup. No other component calls `Program`.
- `appsettings.json` adds `SeedAdmin:UserName`. No existing reader of that key.
- `DatabaseInitializer.InitializeAsync` is unchanged. Indexed relationships: none (call site is top-level `Program.cs`). Tests that depend on empty Identity tables stay valid because they do not call the seeder.
- `AddSymphonyDatabase` already registers a scoped `SymphonyDbContext` (covered by `AddSymphonyDatabase_SqliteProvider_RegistersFactoryAndContext`). Seeding uses that registration; it does not change provider selection.
- Out of scope: login UI (T4), workflow seed, app settings seed, migrations, Identity UI, password-policy changes, multi-instance locking beyond the duplicate-username race handling above.

## GitHub issues log

- (none yet)
