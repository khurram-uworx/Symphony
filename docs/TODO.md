# T1: Convert Symphony to an ASP.NET Core MVC web host

Issue: [#6](https://github.com/khurram-uworx/Symphony/issues/6)
Branch: `khurram/6`

As each task executes, if deferred work or a concern appears outside this plan, create a GitHub issue immediately (`gh issue create --repo khurram-uworx/Symphony`) and record its number in the GitHub issues log. Do not hold it in memory until the end.

## Problem

`src/Symphony.App` is a console `Host`. Startup calls `WorkflowPathResolver.Resolve(args)`, which throws unless a `WORKFLOW.md` exists. That path is a constructor dependency of `WorkflowManager`, and the rest of the candidate-polling graph (`ServiceConfigProvider`, tracker/agent factories, `WorkspaceManager`, `AgentRunner`, `OrchestratorService`) cannot be constructed without it. The factories also call `StartWatching()` and `GetConfig()` at resolution time.

The product entry point needs to be a web host that starts Kestrel and answers `/` without a workflow file. The poller stays compiled until T51. It is not registered.

Accepted extras beyond the issue text:

- Rename the app project to Symphony: folder `src/Symphony`, project `Symphony.csproj`, assembly and root namespace `Symphony`. Child namespaces drop `.App`.
- Use MVC (controllers and views), not Razor Pages. Official docs prefer Razor Pages as the default page UI; this is an explicit override for controller/view separation. Do not re-open it.

## Proposed changes

### 1. Rename the app project

`git mv` `src/Symphony.App` to `src/Symphony` and `Symphony.App.csproj` to `Symphony.csproj`.

Replace `Symphony.App` with `Symphony` in app source (`namespace`, `using`, and the fully qualified `ShellCommandRunner` reference). Update:

- `Symphony.slnx`
- `tests/Symphony.Tests/Symphony.Tests.csproj`

Do not edit `.github/workflows`. CI builds `Symphony.slnx` and the test project, not a hardcoded app path.

Set `<RootNamespace>Symphony</RootNamespace>` so new MVC files match the renamed namespaces.

### 2. Web SDK and MVC host

`Symphony.csproj`: SDK `Microsoft.NET.Sdk.Web`, keep `net10.0`, drop `<OutputType>Exe</OutputType>`. The Web SDK references the shared ASP.NET Core framework, so no extra MVC package is required. Drop the explicit `Microsoft.Extensions.DependencyInjection`, `Hosting`, `Http`, and `Logging` package references; the shared framework supplies them and pinning 10.0.13 warns NU1510. Keep the other existing packages. Do not add Identity or EF.

`Program.cs` becomes a `WebApplication` builder. Do not call `WorkflowPathResolver.Resolve`. Do not register `WorkflowPath`, `WorkflowManager`, `ServiceConfigProvider`, `WorkspaceManager`, the tracker/agent factories, `AgentRunner`, or `OrchestratorService`. Those registrations are not valid without a workflow file, and registering them would start candidate polling. Leave the types in the project. `WorkflowPath` stays because `WorkflowManager` still takes it.

Keep the existing single-line console logger on the web builder.

Official .NET 10 shape ([AddControllersWithViews](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.mvcservicecollectionextensions.addcontrollerswithviews), [MVC routing](https://learn.microsoft.com/aspnet/core/mvc/overview?view=aspnetcore-10.0#routing)):

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

`UseRouting` is added by the host when endpoints are mapped. Do not add `UseHttpsRedirection`, HSTS, or `UseExceptionHandler("/Home/Error")` in this bootstrap. Those need a dev certificate or an Error action that this cut does not have, and they are not required for `/` to return a view. `dotnet run` must start Kestrel on HTTP without trusting a certificate.

Do not call `AddRazorPages` or `MapRazorPages`.

### 3. Placeholder MVC page

- `src/Symphony/Controllers/HomeController.cs` — `sealed`, `Index()` returns `View()`. No business logic.
- `src/Symphony/Views/_ViewImports.cshtml` — `@using Symphony` and the MVC tag helpers.
- `src/Symphony/Views/_ViewStart.cshtml` — layout.
- `src/Symphony/Views/Shared/_Layout.cshtml` — minimal document that renders the body.
- `src/Symphony/Views/Home/Index.cshtml` — short placeholder that the host is running.

No `wwwroot`, no Identity, no error page.

`Properties/launchSettings.json` uses an HTTP-only `http` profile (`http://localhost:5080`) so `dotnet run` does not depend on the HTTPS dev certificate. No `launchBrowser`.

Minimal `appsettings.json` and `appsettings.Development.json` for logging only. No connection strings.

### 4. Operator docs

`README.md`: Getting Started and Running start the web host with `dotnet run --project src/Symphony`. Do not document `dotnet run -- path/to/WORKFLOW.md` as how to run Symphony. The sample `WORKFLOW.md` may still be mentioned as a file, not as a launch argument. Project structure uses `src/Symphony/` and notes Controllers/Views. Soften the overview so console polling is not the way to start the product. Legacy polling removal remains T51.

`AGENTS.md`: repository map describes `src/Symphony/` as the web-host entry point and MVC as the UI shape. Do not document console polling as how to run the app.

Do not rewrite `docs/plans/`. That drift is #21.

## Verification

- `dotnet build Symphony.slnx` after the rename and again after the web host.
- Ask before `dotnet test`. The execute-plan instruction authorizes testing the branch and the PR test plan after the PR exists.
- After the web host lands, `dotnet run --project src/Symphony` starts Kestrel with no workflow argument and no `WORKFLOW.md` required. `GET /` returns the MVC home view (HTTP 200, body contains the placeholder text).
- `README.md` and `AGENTS.md` no longer tell an operator to start the product with a workflow file.
- G2 reviews the branch against #6 and this file before `docs/TODO.md` is deleted.
- DocsCoveragePins: none exist in the repo. Re-check at G2 and revise only if a pin file or test appears.

## Planned commits

1. `docs: plan T1 web host conversion in TODO.md`
2. `Rename the app project to Symphony`
3. `Host Symphony as an ASP.NET Core MVC app`
4. `Document the web host as the product entry point`
5. `docs: remove TODO.md — plan executed` after G2 clears

## Blast radius

- Files: every `.cs` under `src/Symphony.App` (namespace text), the csproj, `Symphony.slnx`, the test project reference, `Program.cs`, new MVC files, `README.md`, `AGENTS.md`.
- Symbols: `WorkflowPathResolver.Resolve` loses its only caller (`Program.cs` is top-level and is not a symbol). `WorkflowPath` remains referenced by `WorkflowManager`. No test code references `Symphony.App` types. Probe and performance projects do not reference the app.
- Downstream: solution and `Symphony.Tests` project reference must move or the build fails. CI workflows do not hardcode the app path.
- Not touched: poller types, GitHub workflows, Identity/EF, `docs/plans/`.

## GitHub issues log

- [x] #21 — Update web plan docs for Symphony rename and MVC (known before execution; T4 #9 still specifies Razor Pages under `src/Symphony.App/Pages/`)
