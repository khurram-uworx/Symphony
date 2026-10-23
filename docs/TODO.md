# Plan: T10 Member account area and greeting + UI polish

## Problem
- Issue #19 asks for a member-facing `/account` area with personalized greeting, role display, navigation placeholders, and server-side enforcement that members cannot access `/admin/*`.
- Current `AccountController` only handles Login/Logout/AccessDenied; there is no `Index` action or `Views/Account/Index.cshtml`.
- Shared layout has minimal nav and no Account link.
- Forms (Login, Admin/Users) are constrained by `main { width: min(440px, ...) }` and several controls (select, table, buttons) are not consistently styled, making the UI look narrow/unfinished.

## Proposed changes
1. `src/Symphony/Controllers/AccountController.cs`
   - Add `UserManager<IdentityUser>` to primary constructor.
   - Add `[HttpGet] [Authorize] Index()` returning display name + roles via `ViewData`.
2. `src/Symphony/Views/Account/Index.cshtml` (new)
   - Greeting, display name, role, placeholder shortcuts.
3. `src/Symphony/Views/Shared/_Layout.cshtml`
   - Add Account link for authenticated users; keep Admin link role-gated.
4. `src/Symphony/wwwroot/css/site.css`
   - Widen main container for account/admin screens, improve form controls, tables, nav, responsive layout.
5. Verify AdminController remains role-protected.

## Verification
- `dotnet build Symphony.slnx`
- Run host and Playwright checks for admin/member flows.
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (ask before running).

## Planned commits
1. docs: plan T10 in TODO.md
2. feat: add account area index and greeting
3. feat: improve shared navigation
4. style: polish forms and layout
5. docs: remove TODO.md — plan executed

## Blast radius
- AccountController constructor change affects DI resolution only; no callers besides framework.
- New view only.
- CSS changes affect all pages.
- Admin protection unchanged.

## GitHub issues log
- (none yet)
```