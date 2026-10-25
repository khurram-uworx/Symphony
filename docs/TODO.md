# Plan: Milestone 7 (M7) - Admin controls, recovery, live updates, settings, and provider tests

Branch: khurram/milestone7
Base: main
Decision: T44 uses Option A (workflow name from original request/snapshot) for reproducibility; link via RetryOfJobId.

## Problem
Implement M7 tasks T44–T49 per docs/plans/SYMPHONY-WEB-PLAN.md:
- T44: Admin retry linked jobs (decision gate) - POST /api/jobs/{id}/retry, create linked job using original snapshot/workflow (Option A), store RetryOfJobId, reuse encrypted PAT from snapshot if present.
- T45: Cancellation, stale recovery, and cleanup - improve cancellation, recover stale running jobs on startup as Failed with "interrupted-run" event, workspace cleanup best-effort.
- T46: Real-time job updates with SignalR - add JobUpdateHub with groups by user/admin, push updates, keep polling fallback.
- T47: Admin settings and app settings API/page - /admin/settings to manage global settings (concurrency, default workspace root, retry backoff), processor picks up changes.
- T48: Redaction and encrypted snapshot access - ensure snapshot/PAT never leak via APIs/UI/logs/events.
- T49: Provider matrix tests - schema init/migrations/minimal flows across SQLite/SQL Server/PostgreSQL when available (CI passes without external DBs).

## Grounding (G1) plan
- microsoft-learn: ASP.NET Core SignalR, job queues/background services, Data Protection redaction patterns
- code-memory: inspect JobService, JobProcessor, JobEndpoints, AdminController, Data models, existing patterns

## Blast radius
- API: JobEndpoints (add retry), possibly new endpoints
- Services: JobService (add retry/create from snapshot), AppSettingsService (if exists or create)
- Processing: JobProcessor (stale recovery, cancellation tracking, settings reload)
- Hubs: new JobUpdateHub
- Controllers/Views: AdminController + views for jobs/settings
- Security/Redaction: ensure no leaks
- Tests: add provider matrix tests

## Planned commits (one logical change per commit)
1. docs: plan M7 in TODO.md
2. feat(api/jobs): add admin retry endpoint creating linked job from original snapshot (T44, Option A)
3. feat(processing): improve cancellation, stale recovery on startup, workspace cleanup (T45)
4. feat(hubs): add SignalR JobUpdateHub for live updates with groups (T46)
5. feat(admin/settings): add app settings API and admin settings page (T47)
6. fix(security): strengthen redaction for snapshot/PAT and ensure DTOs exclude them (T48)
7. test(integration): add provider matrix tests (T49)
8. docs: remove TODO.md after G2 passes

## Verification steps
- dotnet build Symphony.slnx
- Ask before running tests: dotnet test tests/Symphony.Tests (default filter per AGENTS.md)
- Manual/Playwright smoke later as instructed

## GitHub issues log
- [ ] (none yet)
