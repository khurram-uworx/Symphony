# Plan: T9 Admin user and role management (issue #18)

## Problem

Admins need to create, disable, delete, and assign roles to users through `/admin/users`. Today `AdminController` is only a placeholder and there is no backing service. Cookie-auth sessions must be invalidated when a user is disabled or deleted.

## Proposed changes

- Add `src/Symphony/Services/UserManagementService.cs` (sealed, primary constructor) with create/disable/enable/delete/reset-password/change-role/session-invalidation/list/get operations. Static + instance methods for testability.
- Extend `AdminController` with `Users`, `CreateUser`, `DisableUser`, `EnableUser`, `DeleteUser`, `ResetPassword`, `ChangeRole` actions.
- Add `Views/Admin/Users.cshtml` with table and forms.
- Register service in `Program.cs`.
- Set `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` in `IdentityRegistration` for immediate session invalidation.
- Update layout Admin link to `/Admin/Users`.

## Verification steps

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (ask first)
- Playwright smoke of create/login/disable/delete flow against `dotnet run`.

## Planned commits

1. docs: plan T9 in TODO.md
2. feat: add UserManagementService
3. feat: add admin user management controller actions and view
4. feat: register service and harden session invalidation
5. docs: remove TODO.md — plan executed

## Blast radius

- Files: AdminController, IdentityRegistration, Program, new service/view.
- Symbols: AdminController (new actions), UserManagementService (new).
- Downstream: Admin nav link.
- Tests: AdminSeederTests unaffected; new service tests may be added.

## GitHub issues log

- (none yet)
