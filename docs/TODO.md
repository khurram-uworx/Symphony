# Plan: T22 Job submission API (#46)

## Problem

Implement `POST /api/work` to queue explicitly submitted work. Depends on T20/T21/T24; T24 (`JobQueue`) is absent, so this plan includes the minimal wake-up signal.

## Proposed changes

- `src/Symphony/Dto/WorkRequestDtos.cs`: request DTOs (`externalId`, `ticketIds`, `workItems`, nested `github.repository`/`personalAccessToken`, `workflowName`, `overrides`).
- `src/Symphony/Services/JobService.cs`: validate, resolve owned workflow, persist `Job (Queued)`, encrypted snapshot, wake processor.
- `src/Symphony/Processing/JobQueue.cs`: notification-only wake primitive (T24 scope).
- `src/Symphony/Api/WorkEndpoints.cs`: minimal API endpoint returning 202 `{ jobId, statusUrl }`.
- `src/Symphony/Program.cs`: DI registration and endpoint mapping.

## Verification

- build solution
- targeted dotnet tests (human confirm)
- manual smoke with real host + Playwright/curl

## Planned commits

1. docs: plan T22 in TODO.md
2. feat: add JobQueue wake-up signal (T24 minimal)
3. feat: add WorkRequest DTOs
4. feat: add JobService
5. feat: add WorkEndpoints and wiring
6. docs: remove TODO.md — plan executed (after G2)

## GitHub issues log

- None captured.