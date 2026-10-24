# T29: Background job processor

## Problem
Implement background processor for queued jobs with recovery, concurrency, per-issue execution, persistence, user-scoped workspaces.

## Proposed changes
- src/Symphony/Processing/JobProcessor.cs: BackgroundService.
- src/Symphony/Services/JobService.cs: persistence helpers.
- src/Symphony/Config/ServiceConfigFactory.cs: use as-is.
- src/Symphony/Workspaces/WorkspaceManager.cs: use scoped configs via JobProcessor.
- src/Symphony/Program.cs: register hosted service.
- tests/Symphony.Tests/Processing/JobProcessorTests.cs: unit-ish recovery/polling.

## Planned commits
1. docs: plan T29 in TODO.md
2. feat: add JobProcessor hosted service
3. feat: add JobService persistence helpers
4. feat: register JobProcessor and integration glue
5. test: JobProcessor recovery and inline job
6. docs: remove TODO.md

## GitHub issues log
- (none yet)
