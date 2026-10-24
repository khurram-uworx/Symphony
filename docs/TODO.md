# Plan: T14 Configuration web page

## Problem
Provide `/account/configuration` MVC form for editing personal work tool and agent setup.
No `Configuration` action exists on `AccountController` and no `Views/Account/Configuration.cshtml` view.

## Proposed changes
- `src/Symphony/Controllers/AccountController.cs`: add optional `ConfigurationService` dependency, `GetUserIdAsync`, GET/POST `Configuration` actions.
- `src/Symphony/Views/Account/Configuration.cshtml`: MVC form covering tracker settings, states, workspace root, hooks, concurrency/turns, Copilot BYOM, default workflow name.
- Reuse `ConfigurationDto`, `Redaction`, `ConfigurationService`.

## Verification
- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Manual Playwright check of `/account/configuration` after login.

## Planned commits
1. docs: plan T14 in TODO.md
2. feat: add Configuration actions to AccountController
3. feat: add Configuration view
4. test/docs follow-ups as needed
5. docs: remove TODO.md — plan executed

## Blast radius
- AccountController constructor (optional new param; backward compatible)
- New view route /account/configuration
- Uses existing ConfigurationService and DTOs; no DB/schema changes.

## GitHub issues log
- (none yet)
