# T12: UserConfiguration persistence and encryption

## Problem

Persist per-user `UserConfiguration`, including tracker, agent, workspace, hooks, and concurrency settings. Each user owns a saved configuration used to build runtime `ServiceConfig`. Secret fields must be encrypted at rest via `ISecretProtector` (T19), with a 1:1 relationship to the user and created/updated timestamps.

## Proposed changes

- Add `ISecretProtector` / `SecretProtector` (T19) wrapping ASP.NET Core Data Protection with a dedicated purpose string.
- Add `Redaction` helpers.
- Add `UserConfiguration` entity in `Symphony.Data` (with a `Symphony.Domain.UserConfiguration` type alias for compatibility).
- Add `DbSet<UserConfiguration> UserConfigurations` and fluent 1:1 mapping to `SymphonyDbContext`.
- Add `ConfigurationService` that creates/updates per user and protects/unprotects secrets.
- Generate migrations for Sqlite, SqlServer, and PostgreSQL.
- Add tests.

## Verification steps

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (after human confirmation)
- Inspect generated migration SQL/table columns.

## Planned commits

1. `docs: plan T12 in TODO.md`
2. `feat: add secret protection service (T19)`
3. `feat: add UserConfiguration entity and DbContext mapping`
4. `feat: add ConfigurationService with encrypted secrets`
5. `feat: add UserConfiguration migrations`
6. `test: cover secret protection and UserConfiguration persistence`
7. `docs: remove TODO.md — plan executed`

## Blast radius

- `Symphony.Data.SymphonyDbContext` gains a new entity set and `OnModelCreating`.
- Three migration projects gain new migrations/snapshots.
- `Program.cs` registers `ISecretProtector`.
- New `ConfigurationService` depends on `IDbContextFactory<SymphonyDbContext>` and `ISecretProtector`.

## GitHub issues log

- (none yet)