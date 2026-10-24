# Plan: Configured first-run admin password (#82)

## Problem

`AdminSeeder.SeedAsync` always generates the first admin password and prints it to host stdout once. After a restart, or when stdout is not retained, the credential is unrecoverable and the only way back in is deleting the database. Anyone verifying the UI (humans, Playwright-driven agents) has to scavenge shell history for the password.

## Proposed changes

### 1. `src/Symphony/Data/AdminSeeder.cs`

Add `string? configuredPassword = null` to `SeedAsync` before the `CancellationToken` so existing positional callers keep compiling.

- Treat null/whitespace as "not configured"; never trim a real password.
- `var password = configured ?? generatePassword(users.Options.Password);`
- When `CreateAsync` fails and a password was configured, throw `InvalidOperationException` naming `SeedAdmin:Password` and the Identity error codes. Do not include the password value.
- Output: generated password keeps `Password: <value>`; configured password prints `Password: (configured via SeedAdmin:Password)` so logs never leak a deployment secret.
- No change to duplicate-username recovery, role assignment, default workflow seeding, or the existing-admin early return.

### 2. `src/Symphony/Program.cs`

Pass `builder.Configuration["SeedAdmin:Password"]` as the configured password. This also picks up `SeedAdmin__Password` from environment/container config for free.

### 3. Configuration

- `src/Symphony/appsettings.json`: add `"Password": ""` to `SeedAdmin` with a comment that empty means generate-and-print-once.
- `src/Symphony/appsettings.Development.json`: set the known development password `Symphony!dev1`. This file is only loaded with `ASPNETCORE_ENVIRONMENT=Development`, which `launchSettings.json` sets, so the published image never ships a usable credential.

### 4. Tests — `tests/Symphony.Tests/Data/AdminSeederTests.cs`

Extend the `seed(...)` helper with an optional password, then add:

- `SeedAsync_ConfiguredPassword_CreatesAdminWithThatPassword` — `CheckPasswordAsync` succeeds, stdout contains the configured marker and never the secret.
- `SeedAsync_InvalidConfiguredPassword_ThrowsNamingConfigKey` — weak value throws, message names `SeedAdmin:Password`, no user persisted, no secret printed.
- `SeedAsync_BlankConfiguredPassword_FallsBackToGenerated` — whitespace behaves like the generated path.

Existing tests stay green, in particular `SeedAsync_NoAdmin_CreatesRolesAdminAndWritesCredentials` which asserts exactly one `Password:` line.

### 5. Documentation

- `AGENTS.md` smoke-check paragraph: state the development credentials and that deleting `src/Symphony/App_Data/symphony.db` reseeds them.
- `README.md` environment variable list: add `SeedAdmin__Password`.
- `CONTAINER-README.md`: env table row plus the configured-output note.

## Verification

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Manual: delete `src/Symphony/App_Data/symphony.db`, `dotnet run --project src/Symphony`, sign in at `http://localhost:5080` with `testadmin` / `Symphony!dev1`, then exercise the workflows list and editor through Playwright.

## Planned commits

1. `docs: plan configured seed admin password in TODO.md`
2. `Support a configured first-run admin password`
3. `Document the seeded development admin password`
4. `docs: remove TODO.md — plan executed`

## Blast radius

- `AdminSeeder.SeedAsync` — one method; runtime caller is `Program.cs`, test caller is the `seed(...)` helper in `AdminSeederTests`.
- Startup only. No request-path code, no schema change, no migration.
- `IdentityRegistration` is untouched: the configured password is validated by the existing `UserManager.CreateAsync` against stock Identity options.
- Docs touched: `AGENTS.md`, `README.md`, `CONTAINER-README.md`.

## GitHub issues log

- [x] #82 — configured first-run admin password (this plan)
