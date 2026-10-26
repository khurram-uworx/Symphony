# Plan for issue #106 — full-page job detail view with tabbed telemetry

## Problem
- Job detail is dialog-based on the account page, inconsistent with the dedicated admin route `/admin/jobs/{id}`.
- Telemetry truncation is unclear: record counts and "truncated" messaging do not account for caps correctly.
- Job listings are inconsistent across Account/Index (jobs tab), Run page, and Admin jobs.

## Proposed changes
- Backend telemetry retention/ordering in `JobTelemetryCapture.ReadAsync`: sort newest-first, retain the newest 100, mark `Truncated` when more than 100 records exist.
- Add an account job detail route/view at `/Account/JobDetail/{id:guid}` with Details/Telemetry tabs, matching the admin view.
- Convert `Views/Admin/JobDetail.cshtml` to tabbed Details/Telemetry.
- Update account job View controls to link to the dedicated detail route.
- Remove the redundant Jobs tab and job polling from `Views/Account/Index.cshtml`.
- Improve the Run page job listing to link View to the dedicated detail route.
- Keep admin jobs as the all-users mixed list with owner and View links.

## Planned commits
1. docs: plan issue #106 in TODO.md
2. fix(telemetry): retain newest 100 telemetry records
3. feat(ui): add tabbed admin job detail view
4. feat(routes): add account job detail route and view
5. refactor(ui): consolidate job listings
6. test/verify: standard unit test filter
7. docs: remove TODO.md after reviews

## Blast radius
- Backend: JobTelemetryCapture, JobTelemetryBundle serialization, API `/api/jobs/{id}/telemetry`
- Views: Admin/JobDetail, Admin/Jobs, Account/Index, Account/Run, new Account/JobDetail
- Controller: AccountController (new JobService dependency and JobDetail action)
- No database migration required

## Verification
- dotnet build Symphony.slnx
- dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness" (after approval)
- Playwright smoke for admin and account detail tabs and telemetry

## GitHub issues log
- [ ] #106 — full-page tabbed job detail view and last-100 telemetry display (in progress)
