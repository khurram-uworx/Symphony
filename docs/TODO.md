# Plan: Update web plan docs for Symphony rename and MVC (#21)

Issue: https://github.com/khurram-uworx/Symphony/issues/21
Branch: `khurram/21`

As each task executes, if deferred work or a concern appears that is outside this plan, create a GitHub issue immediately (`gh issue create --repo khurram-uworx/Symphony`) and record its number in the GitHub issues log below. Do not rely on memory or wait until the plan finishes.

## Problem

T1 (#6, closed) renamed the app project from `src/Symphony.App` to `src/Symphony` and adopted MVC (`AddControllersWithViews`, `HomeController`, `Views/`). The product plans still describe Razor Pages under `src/Symphony.App/Pages/`. Later tasks, especially #9 (T4), must not be implemented as written.

#6 updated `README.md` and `AGENTS.md` only. This plan does not touch those files, and it does not change application code.

## Decisions already cleared

- Login is a custom MVC `AccountController` plus `Views/Account/Login.cshtml`. No `Microsoft.AspNetCore.Identity.UI`, no `MapRazorPages()`, no `Areas/Identity/Pages`. Cookie authentication stays.
- Update every open issue body that still names `Symphony.App`, Razor Pages, or `Pages/`: #7, #8, #9, #10, #11, #12, #13, #15, #16, #18, #19, #20. Update #9 in place; do not supersede it.
- Leave closed #6 as the historical T1 record.
- No MVC Areas. Match the landed host: `Controllers/` + `Views/` and the existing conventional route.
- Screen words such as "admin job pages" stay. The ban is the Razor Pages framework and the `Pages/` folder.

## Blast radius

Docs and GitHub issue bodies only. No symbol, caller, or test in `src/Symphony` or `tests/Symphony.Tests` changes.

- `docs/plans/SYMPHONY-WEB.md` — product plan. Sections 3, 9, 11, and 15.
- `docs/plans/SYMPHONY-WEB-PLAN.md` — task breakdown. Path rewrite plus UI task file lists.
- `docs/plans/GITHUB-ISSUES.md` — one tracker-client path.
- GitHub issues #7–#13, #15, #16, #18–#20 — bodies only.
- Downstream: future agents implementing T2+ will read these files and issue bodies. Wrong paths here cause them to create `src/Symphony.App` and `Pages/`.
- Not touched: `README.md`, `AGENTS.md`, `src/Symphony/**`, tests, workflows, `opencode.json` (local Playwright MCP config; leave unstaged).
- No tests cover markdown. Verification is search, not `dotnet test`.

## Proposed changes

### 1. `docs/plans/SYMPHONY-WEB.md`

- Section 3: replace "Identity UI can be used for first cut, or later replaced with custom pages." with cookie auth plus a custom MVC login and logout (`AccountController`, `Views/Account/Login.cshtml`). Do not scaffold Identity UI.
- Section 9: replace "Recommended first UI implementation: Razor Pages." with MVC controllers and views, matching the host that already calls `AddControllersWithViews()`. Keep the URL list (`/admin`, `/account/...`). Leave Blazor as a later evaluation, not the first UI.
- Section 11: the SDK change already landed. Point remaining package and bootstrap work at `src/Symphony/Symphony.csproj` and `src/Symphony/Program.cs`. Do not instruct a conversion of `Symphony.App`.
- Section 15: add a resolved decision — host is `src/Symphony`; UI is MVC controllers and views, not Razor Pages or Identity UI.

### 2. `docs/plans/SYMPHONY-WEB-PLAN.md`

Path rewrite, in this order so the project file name does not survive the prefix replace:

1. `src/Symphony.App/Symphony.App.csproj` → `src/Symphony/Symphony.csproj`
2. `src/Symphony.App/` → `src/Symphony/`
3. `dotnet run --project src/Symphony.App` → `dotnet run --project src/Symphony`
4. Remaining prose `Symphony.App` → the landed project name `Symphony` / `src/Symphony`

On T1, add one line that the rename and MVC shell already landed in #6. Do not rewrite the closed task's scope.

UI tasks keep routes and acceptance criteria. Replace `Pages/` and `.cshtml.cs` page models:

| Task | Files |
|---|---|
| T4 | existing `HomeController` + `Views/Home/Index.cshtml`; `AccountController` + `Views/Account/Login.cshtml`; `AdminController` + `Views/Admin/Index.cshtml`; existing `Views/Shared/_Layout.cshtml`; `Program.cs` |
| T9 | `AdminController` users actions + `Views/Admin/Users.cshtml` + `Services/UserManagementService.cs` |
| T10 | `AccountController` + `Views/Account/Index.cshtml`, shared layout, `Views/Admin/Index.cshtml` |
| T14, T17, T23, T39, T41 | `AccountController` actions + `Views/Account/{Configuration,Workflows,Run,ApiKeys}.cshtml` |
| T33, T46, T47 | `AdminController` + `Views/Admin/{Jobs,JobDetail,Settings}.cshtml` |
| T51 | `Views/**/*.cshtml` |

T4 suggested path: custom MVC login, not "Use Razor Pages." Cookie `LoginPath` is `/Account/Login`, which matches the landed conventional route `{controller}/{action}/{id?}`.

T33 suggested path: MVC action query binding, not "Razor Pages with query binding."

### 3. `docs/plans/GITHUB-ISSUES.md`

`src/Symphony.App/IssueTracker/GitHubIssuesClient.cs` → `src/Symphony/IssueTracker/GitHubIssuesClient.cs`.

### 4. GitHub issue bodies

Same path rules. #9, #18, and #19 also lose Razor Pages and `Pages/`, using the file mapping above. #9 keeps login, greeting, `/admin` placeholder, cookie redirect, and logout. Add a note that the Razor Pages wording was superseded by #21.

## Verification

- `rg` in `docs/plans` for `Symphony.App`, `Razor Pages`, `Razor Page`, and `Pages/`. Expect no matches. The English word "page" in screen descriptions may remain.
- `gh issue view` for #7–#13, #15, #16, #18–#20: no `Symphony.App`, no `Razor`, no `Pages/`.
- Do not run `dotnet test`. No code changes.

## PR test plan

After the PR exists, re-check the same searches against the branch. This change does not alter the host. If a container test is run, rebuild images rather than reusing a stale image, then confirm `/` still returns the existing MVC landing view. Do not add a Dockerfile in this issue; T8 owns that.

## Planned commits

1. `docs: plan issue 21 web plan MVC update in TODO.md`
2. `docs: point the web product plan at src/Symphony and MVC`
3. `docs: point the task breakdown at MVC controllers and views`
4. `docs: point the GitHub Issues tracker plan at src/Symphony`
5. GitHub issue body updates (no repo commit)
6. `docs: remove TODO.md — plan executed` after G2 clears

## GitHub issues log

- #21 — this plan. Do not create a duplicate.
