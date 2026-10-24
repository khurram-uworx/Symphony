# T15: Workflow persistence

## Problem

Named, user-owned workflows are not persisted yet. Downstream tasks (T16 CRUD API, T17 editor, T18 seeding, T20+) cannot resolve workflow content by name without this foundation.

Issue: https://github.com/khurram-uworx/Symphony/issues/39

## Proposed changes

### Data
- Add `src/Symphony.Data/Workflow.cs` entity with Id, OwnerUserId, Owner navigation, Name, Content, IsDefault, CreatedAt, UpdatedAt.
- Add `DbSet<Workflow> Workflows` and fluent mapping in `SymphonyDbContext` including unique index on (OwnerUserId, Name).
- Add `src/Symphony/Domain/Workflow.cs` alias.

### Service
- Add `WorkflowService` with ListAsync/GetAsync/CreateAsync/UpdateAsync/DeleteAsync/ExistsAsync and aliases.
- Trim names, case-insensitive OrdinalIgnoreCase lookup/uniqueness.
- `WorkflowConflictException : InvalidOperationException` with Code.
- Validate empty name/content; catch DbUpdateException on create.

### Migrations
- `AddWorkflow` in Sqlite, SqlServer, PostgreSQL migration projects.

### Tests
- Add `WorkflowPersistenceTests.cs`.

## Verification
- `dotnet build Symphony.slnx`
- run unit tests after approval
- migrations create Workflows table and unique index.

## Planned commits
1. docs: plan T15 workflow persistence in TODO.md
2. feat: add Workflow entity and DbContext mapping
3. feat: add WorkflowService with persistence rules
4. chore: add AddWorkflow migrations for all providers
5. test: cover Workflow persistence rules
6. docs: remove TODO.md — plan executed

## Blast radius
- New table only. No existing callers. New tests use SQLite temp db.

## GitHub issues log
- (none yet)
