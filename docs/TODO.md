# T17: Workflow web editor

## Problem

`/account/workflows` is advertised in `Account/Index.cshtml` as "coming soon" but does not exist.
Workflow CRUD already exists under `/api/workflows` (T16), but there is no MVC page to create,
edit, rename, copy, and delete workflows. Users must call the API manually.

## Proposed changes

### 1. Backend rename support

- `src/Symphony/Services/WorkflowService.cs`:
  - Update `UpdateAsync(string, string, Workflow, CancellationToken)` so a changed
    `workflow.Name` renames the stored workflow instead of throwing "first cut" unsupported.
  - Normalize and validate the requested name, reject duplicate names against other workflows
    for the same owner, and persist name/content/default in one save.
- `src/Symphony/Api/WorkflowEndpoints.cs`:
  - Remove the rename rejection branch.
  - Send a `Workflow` instance (including `Name`) to the service overload.
  - Keep 409/404/400 handling.

### 2. MVC page and bundled template

- `src/Symphony/Controllers/AccountController.cs`:
  - Add optional `IWebHostEnvironment? environment`.
  - Add `[HttpGet] [Authorize] Workflows(CancellationToken)` returning `View("Workflows")`.
  - Load `WORKFLOW.md` from content root when available and place it in
    `ViewData["WorkflowTemplate"]`. Degrade to an empty template in test hosts.
- `src/Symphony/Views/Account/Workflows.cshtml` (new):
  - Account page shell matching existing `panel` layout.
  - Master/detail editor:
    - sidebar list with default badge.
    - editor with name, default toggle, metadata, textarea, Save and Delete.
    - "New blank" and "Copy from WORKFLOW" actions.
  - JavaScript uses only `/api/workflows` (`GET`, `POST`, `PUT`, `DELETE`).
  - Separate tracked original name to support rename.
  - Disable Delete when one workflow remains; handle API `409 last_workflow`.
  - Embed template via JSON serialization.
- `src/Symphony/Views/Account/Index.cshtml`:
  - Remove "coming soon" text from Workflows shortcut.

### 3. Sleek styling

- `src/Symphony/wwwroot/css/site.css`:
  - Append responsive master/detail grid, list selection, default badge, monospace textarea,
    secondary/danger buttons, status messages, empty/loading states.
  - Preserve existing dark-theme tokens.

## Acceptance criteria

- [ ] `/account/workflows` lists workflows and opens an editor.
- [ ] New workflow can be created from blank.
- [ ] Workflow can be copied from bundled `WORKFLOW`.
- [ ] Existing workflow content can be edited and saved.
- [ ] Workflow can be renamed by changing the name and saving.
- [ ] Delete requires confirmation and cannot remove the last workflow.
- [ ] Default flag is shown/edited.
- [ ] Content round-trips unchanged.
- [ ] No direct DB access from the view; page uses `/api/workflows`.
- [ ] All non-Integration/Smoke/ReleaseReadiness tests pass.

## Verification steps

- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`
- Launch app with `dotnet run --project src/Symphony` and exercise create/edit/rename/copy/delete in browser.

## Planned commits

1. docs: plan T17 workflow web editor in TODO.md
2. feat: support workflow rename in WorkflowService and API
3. feat: add Workflows MVC page consuming workflow API
4. style: add sleek workflow editor styles
5. docs: remove TODO.md — plan executed

## Blast radius

- `WorkflowService.UpdateAsync` callers that switch to the `Workflow` overload: mainly
  `WorkflowEndpoints.UpdateAsync`. Existing string-based `UpdateAsync` overloads remain.
- `AccountController` constructor callers gain an optional environment parameter; default is
  resolved from `HttpContext.RequestServices`, preserving current constructor testability.
- New view depends on `/api/workflows` being mapped (already done in `Program.cs`).
- No database schema changes.

## GitHub issues log

- None captured yet.
