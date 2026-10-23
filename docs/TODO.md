# T4: Minimal authenticated web shell and greeting

GitHub issue: [#9](https://github.com/khurram-uworx/Symphony/issues/9)
Branch: `khurram/9`

As each task executes, if deferred work or a concern shows up outside this plan, create a GitHub issue immediately (`gh issue create --repo khurram-uworx/Symphony`) and record its number in the log below. Do not hold it in memory until the end.

## Problem

M1 needs an authenticated MVC shell. Identity, cookie paths, and the one-time admin seed already exist. There is no login, no greeting, no `/admin` placeholder, and no navigation. `/` is anonymous and says the host is running.

The issue asked for minimal styling. The human amended that: the shell should feel sleek and modern, inspired by Copilot and GitHub, not a copy of Primer, the Copilot mark, or github.com chrome.

## Already done — do not redo

- `IdentityRegistration.AddSymphonyIdentity` sets `LoginPath` `/Account/Login`, `LogoutPath` `/Account/Logout`, `AccessDeniedPath` `/Account/AccessDenied`. Covered by `IdentityRegistrationTests.AddSymphonyIdentity_Default_SetsAccountCookiePaths`.
- `Program.cs` already calls `AddControllersWithViews()`, `AddSymphonyIdentity()`, `UseAuthentication()`, `UseAuthorization()`, and the conventional route. No `MapRazorPages()`. No Identity UI package.
- `AdminSeeder.AdminRole` is `"Admin"`. Default seeded username is `testadmin`. Credentials print once.
- #21 is closed. Do not add a `Pages/` folder.

`Program.cs` is top-level statements, so code-memory has no symbols or call edges for the host wiring. The indexed graph here is `AdminSeeder` internals and its tests. `AccountController` and `AdminController` do not exist.

## Out of scope

- #19 (T10): `/account`, display name and role, configuration placeholders, fuller member navigation.
- #10 (T5): `WebApplicationFactory` smoke tests. After this work, anonymous `/` redirects to login; T5 must not expect 200 from `/`.
- #20 (T11): cookie hardening, HTTPS, HSTS, Data Protection persistence.
- #23: API endpoints and login redirects.
- No self-registration. No remember-me. No new packages.

## Proposed changes

### 1. Account login and logout

`AccountController` is sealed and takes `SignInManager<IdentityUser>` in a primary constructor.

- `GET Login` `[AllowAnonymous]`. If already signed in, redirect as a successful login would. Otherwise render the form and keep `returnUrl`.
- `POST Login` `[AllowAnonymous]` `[ValidateAntiForgeryToken]`. `PasswordSignInAsync(userName, password, isPersistent: false, lockoutOnFailure: true)`. Success: `LocalRedirect` when `Url.IsLocalUrl(returnUrl)`, else `Home/Index`. Failure: one generic model error, redisplay. Do not say whether the username exists.
- `POST Logout` `[Authorize]` `[ValidateAntiForgeryToken]` at `/Account/Logout`. `SignOutAsync()`, then redirect to login. Logout is a POST. Cookie `LogoutPath` does not sign the user out by itself; the ASP.NET Core 10 cookie handler only honors `ReturnUrl` on that path after sign-out.
- `GET AccessDenied` `[AllowAnonymous]`. The cookie handler's `HandleForbiddenAsync` always redirects here. Without an action, a signed-in non-admin hitting `/admin` gets a 404.

`LoginInput` is a sealed class with `UserName` and `Password`, both `[Required]`. Private helpers are camelCase.

```csharp
var result = await signInManager.PasswordSignInAsync(
    input.UserName!,
    input.Password!,
    isPersistent: false,
    lockoutOnFailure: true);

if (result.Succeeded)
    return redirectAfterLogin(returnUrl);

ModelState.AddModelError(string.Empty, "Invalid username or password.");
return View(input);
```

### 2. Greeting

`[Authorize]` on `HomeController`. `Views/Home/Index.cshtml` shows `Hello, {UserName}` from `User.Identity.Name`. Unauthenticated `/` challenges to `/Account/Login?ReturnUrl=...`.

### 3. Admin placeholder

`[Authorize(Roles = AdminSeeder.AdminRole)]` on `AdminController`. Conventional route already serves `/admin`. Placeholder view only. No user management.

### 4. Shell

Inspired by Copilot/GitHub, not a clone:

- Dark developer-tool canvas, one quiet surface for cards, a violet accent used only for the mark, primary action, and focus. Original hex values, not Primer token names and not the Copilot logo.
- System font stack. No CDN, no Bootstrap, no extra package.
- Top bar: wordmark and a small original mark, then Login, or the username, a logout form, and an Admin link when `User.IsInRole(AdminSeeder.AdminRole)`.
- Login is a centered card. Greeting and admin reuse the same card language.
- Labels, `autocomplete`, and `:focus-visible`. Password is not redisplayed.

Static file: `wwwroot/css/site.css`. .NET 10 serves `wwwroot` with `MapStaticAssets()` (build-time fingerprint and compression). Link it with `asp-append-version="true"`. No fallback authorization policy, so the stylesheet stays anonymous. Call `MapStaticAssets()` next to the controller route. Do not add `UseHttpsRedirection` here (#20).

`_ViewImports.cshtml` gains `@using Symphony.Data` so the layout can use `AdminSeeder.AdminRole` instead of a second magic string.

## Blast radius

- New: `AccountController`, `LoginInput`, `AdminController`, `Views/Account/Login.cshtml`, `Views/Account/AccessDenied.cshtml`, `Views/Admin/Index.cshtml`, `wwwroot/css/site.css`.
- Edited: `HomeController`, `Views/Home/Index.cshtml`, `Views/Shared/_Layout.cshtml`, `Views/_ViewImports.cshtml`, `Program.cs` (`MapStaticAssets` only).
- Unchanged: `IdentityRegistration`, `AdminSeeder` behavior, cookie-path test.
- Downstream: no in-repo caller of `HomeController.Index`. Anonymous `/` changes from 200 to a login redirect. Nothing in `tests/Symphony.Tests` asserts the old home text.
- `AdminSeeder.AdminRole` is a public const. Referencing it does not change seeding.

## Verification

- `dotnet build Symphony.slnx` after each implementation commit.
- Ask before `dotnet test`.
- Do not run Integration tests.
- Docker and Playwright are not part of this plan. The human said Docker testing is not yet required.

## Planned commits

1. `docs: plan T4 authenticated web shell in TODO.md`
2. `feat: add account login and logout`
3. `feat: greet the signed-in user`
4. `feat: add a role-protected admin placeholder`
5. `feat: style the shell and add navigation`
6. `docs: remove TODO.md — plan executed` after G2 clears

## GitHub issues log

- [x] #10 — T5 smoke test must expect anonymous `/` to redirect to login, not return 200 (commented on the issue while protecting Home)
- [ ] #19 — member account area stays out of this branch (existing)
- [ ] #20 — cookie hardening and HTTPS stay out of this branch (existing)
