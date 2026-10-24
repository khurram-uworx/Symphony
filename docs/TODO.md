# T23: Run launcher page (issue #47)

## Problem
`/account/run` is referenced from the Account shortcuts (`Views/Account/Index.cshtml`) but has no controller action or view. Members cannot submit work from a friendly UI and must use raw `POST /api/work`.

## Proposed changes
- `AccountController`: add authenticated `Run` GET action; load user config and expose `DefaultWorkflowName` via `ViewData`.
- `Views/Account/Run.cshtml`: tabbed UI (Run / API) that loads workflows via `/api/workflows`, posts the submission payload to `/api/work`, validates client input, and shows job result link.
- `WorkEndpoints`: no behavior change (contract dependency), confirm imports.
- `Views/Account/Index.cshtml`: remove “coming soon” label from the Run launcher shortcut.

## Verification steps
1. `dotnet build Symphony.slnx`
2. Ask human before `dotnet test`.
3. Manual Playwright flow: login, open `/account/run`, submit valid work, verify job appears on status page.
4. Inspect rendered source for PAT/API-key leakage.

## Planned commits
1. `docs: plan T23 run launcher page in TODO.md`
2. `feat: add Account.Run GET action`
3. `feat: add Run launcher tabbed view`
4. `docs: remove TODO.md — plan executed`
(additive fix commits as needed)

## Blast radius
- `AccountController.Run` depends only on existing `ConfigurationService`,
  `GetUserIdAsync`, and view lookup. No changes to constructor signature.
- New Razor view only; no routing changes required (conventional route).
- Client-side submission uses existing `/api/work` and `/api/workflows` contracts.

## GitHub issues log
- (none yet)

### Note
As each task executes, if deferred work or a concern is found, create a GitHub issue immediately via
`gh issue create --repo khurram-uworx/Symphony` and record its number here.
