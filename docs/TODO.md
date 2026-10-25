# System-managed workspace roots plan

## Problem
Workspace root is currently user-owned (`UserConfiguration.WorkspaceRoot`) and may be overridden by workflow files. This is inconsistent and allows path collisions/user overlap. Workspace paths should be system-managed: a single admin-configured root with environment-variable support, per-user folders, and auto-numbered per-repository subfolders to avoid clashes.

## Proposed changes

### Phase 1 — Remove old workspace-root ownership
- Remove `WorkspaceRoot` from `Symphony.Data.UserConfiguration`, `SymphonyDbContext`, and migration models.
- Remove DTO/workspace-root mapping.
- Remove tracker/workspace-root parsing from `ConfigurationService.BuildFromWorkflow`.
- Remove `ServiceConfigFactory.ApplyWorkspaceOverride`; `BuildWorkspace` no longer reads user root.
- `ServiceConfigFactory.Build` accepts optional `systemWorkspaceRoot` used as base.
- Update persistence tests.

### Phase 2 — System-wide workspace root
- Add AppSetting `SystemWorkspaceRoot`, seeded default `%TEMP%/Symphony`.
- Add `SystemWorkspaceRootProvider` (env var `SYMPHONY_WORKSPACE_ROOT`, then AppSetting, then `Path.GetTempPath()/Symphony`).
- Add admin "System" navigation and System page with Configuration tab.
- Add controller save endpoint.

### Phase 3 — Per-user + per-repo folders
- Add `RepositoryWorkspace` entity/table (`Id` auto-increment int, `OwnerUserId`, `Repository`, unique pair).
- Add `RepositoryWorkspaceService.GetOrCreateAsync`.
- Resolve sanitized user name from Identity.
- Update `JobProcessor` to set workspace root to `<SystemRoot>/<user>/<repoNumber>`.

### Phase 4 — Docs and cleanup
- Update docs.
- Keep old app settings unused.

## Verification
- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter ...`
- Playwright smoke for admin System Configuration save and a submitted job showing repo-numbered workspace path.

## Planned commits
1. docs: plan system-managed workspace roots in TODO.md (this file)
2. refactor: remove workspace root from user config and workflow overrides
3. feat: add system workspace root setting provider and admin UI
4. feat: add repository workspace numbering and scoped processing roots
5. test: update tests and docs

## GitHub issues log
- none created yet; capture deferred concerns here as discovered.
