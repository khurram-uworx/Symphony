# T18: Default workflow and app settings seeding

## Problem
Seed the bundled default `WORKFLOW` workflow and default app settings for users/server:
- bundled `WORKFLOW.md` content must be available in published output and used for seed content;
- every user with no workflows gets a workflow named `WORKFLOW`;
- default `AppSetting` values (global concurrency, default workspace root, max retry backoff) are seeded;
- new users are seeded on account creation;
- seeding is idempotent and must never overwrite user edits.

## Proposed changes

1. Add `AppSetting` entity and `DbSet<AppSetting> AppSettings` in `SymphonyDbContext`.
2. Add provider-specific migrations for SQLite/SQL Server/PostgreSQL for `AppSetting`.
3. Embed/publish `WORKFLOW.md` and add a shared accessor for its exact text.
4. Extend `DatabaseInitializer.InitializeAsync` to migrate, then seed defaults and all user workflows.
5. Add idempotent `WorkflowService` seeding methods with conventional aliases.
6. Seed default workflow from user-creation paths (`UserManagementService`, `AdminSeeder`) and lazily on read as a backstop.
7. Use default settings:
   - global concurrency: `10`
   - default workspace root: `workspaces`
   - max retry backoff: `300000`

## Blast radius
- `src/Symphony.Data/*`, `src/Symphony/Data/*`, `src/Symphony/Services/*`
- migrations assemblies
- hidden tests for database init, workflows, app settings, user creation.

## Verification
- build solution
- run non-integration tests after explicit approval
- compare seeded workflow content to bundled `WORKFLOW.md`

## Planned commits
1. docs: plan T18 in TODO.md
2. feat: add AppSetting entity and DbContext support
3. chore: add AddAppSetting migrations
4. feat: add embedded bundled workflow content accessor
5. feat: seed defaults in DatabaseInitializer
6. feat: seed default workflow on user creation
7. test/fix as needed

## GitHub issues log
- none yet