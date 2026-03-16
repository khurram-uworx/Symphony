# ADR-0001: Entity Framework Core multi-provider support

Status: Accepted

## Context

Symphony Web is a multi-user, database-backed application. It must support SQLite by default while also
supporting SQL Server and PostgreSQL through configuration, following the CodeMemory provider-switching
pattern. The active provider is selected from `Database:Provider` and must not be hard-coded in
application startup.

## Decision

- Use one `SymphonyDbContext` with provider-specific EF Core providers.
- Select the provider at runtime from `Database:Provider` (`sqlite`, `sqlserver`, `postgresql`/`postgres`/`npgsql`).
- Register `AddDbContextFactory<SymphonyDbContext>` with the selected provider.
- Connection strings use the keys `Sqlite`, `SqlServer`, and `PostgreSQL`/`Npgsql`.
- SQLite creates its parent directory before schema initialization.
- Schema initialization/migrations are provider-specific; do not assume one migration set is portable.
- Postgres-specific features must be isolated behind provider checks.

## Consequences

- Provider switching is configuration-driven.
- SQLite remains the default for local development.
- SQL Server and PostgreSQL require their own migrations/assemblies.
- Provider-specific code paths must be guarded to keep core mappings portable.