# TODO: T32 Job query/cancellation API + T33 Admin job pages

## Problem

Issues #56 and #57 ask for job status/list/cancellation endpoints with ownership/admin rules and admin job pages with filters, pagination, issue timeline, and event tail. The codebase currently queues and processes jobs but has no query/cancellation API and no admin job pages.

## Plan

### T32 – Job query and cancellation API
- Add `Dto/JobStatusDtos.cs` with masked status DTO.
- Extend `JobService` with query/cancel helpers.
- Add `Api/JobEndpoints.cs`; map in Program.
- Register `JobProcessor` as a resolvable singleton so cancel can cancel the token.

### T33 – Admin job pages
- Add `JobService` query/filter/pagination support used by admin pages.
- Add `Jobs` and `JobDetail` actions to `AdminController`.
- Add `Views/Admin/Jobs.cshtml` and `JobDetail.cshtml`.

## GitHub issues log

- Track any follow-ups discovered during implementation here.

## Verification
- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Playwright MCP smoke against local host.