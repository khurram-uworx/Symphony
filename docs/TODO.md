# T21: Job aggregate persistence

## Problem

Persist `Job`, `JobIssue`, and `JobEvent` aggregates with required indices, per issue #45. No Job tables exist today; downstream tasks (T22 submission, T29 processor, T30 counters/telemetry, T32 query/cancel) depend on this aggregate.

## Proposed changes

Add entities in `Symphony.Data`, EF mappings, Guid IDs, cascade delete issues/events, migrations for all three providers, and Domain aliases.

### Files

- `src/Symphony.Data/Job.cs`
- `src/Symphony.Data/JobIssue.cs`
- `src/Symphony.Data/JobEvent.cs`
- `src/Symphony.Data/SymphonyDbContext.cs` (DbSets + mapping)
- `src/Symphony/Domain/Job.cs`, `JobIssue.cs`, `JobEvent.cs` (aliases)
- `src/Symphony.Migrations.Sqlite/Migrations/*`
- `src/Symphony.Migrations.SqlServer/Migrations/*`
- `src/Symphony.Migrations.PostgreSQL/Migrations/*`

### Entity shape (from `docs/plans/SYMPHONY-WEB.md`)

Job: Id(Guid), ExternalId?, OwnerUserId, WorkflowName, RetryOfJobId?, Repository, EncryptedRequestSnapshot?, State, QueuedAt, StartedAt?, CompletedAt?, SummaryJson?, ErrorCode?, ErrorMessage?, Issues/Events.

JobIssue: Id(Guid), JobId, IssueId?, Identifier?, Title?, State?, Success, Reason?, TurnCount, StartedAt?, CompletedAt?, WorkspacePath?.

JobEvent: Id(Guid), JobId, JobIssueId?, Timestamp, EventName, Summary?, PayloadJson?.

### Indices

- `Job(State, QueuedAt)`
- `Job(OwnerUserId, QueuedAt)`
- `JobIssue(JobId)`
- `JobEvent(JobId, Timestamp)`
- Cascade delete issues/events with job.

### Commit plan

1. docs: plan T21 in TODO.md
2. feat: add Job aggregate entities
3. feat: map Job aggregate in SymphonyDbContext
4. feat: add Domain aliases for Job aggregates
5. chore: add Job aggregate migrations (sqlite, sqlserver, postgresql)
6. test: verify Job aggregate persistence (if added)
7. docs: remove TODO.md — plan executed (at G2)

## Blast radius

- `SymphonyDbContext` gains 3 DbSets and mappings; migration creates 3 tables.
- Any code that depends on `DatabaseInitializer.InitializeAsync` now creates these tables.
- No runtime consumers yet; downstream tasks T22+ will use them.

## Verification

- `dotnet build Symphony.slnx`
- Filtered test command from AGENTS.md (after human approval).

## GitHub issues log

- (none yet)