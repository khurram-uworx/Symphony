# AGENTS.md

## Project overview

Symphony is a .NET 10 web host for AI agent orchestration. The entry point is the ASP.NET Core MVC app in `src/Symphony`. It serves HTTP and does not require a workflow file to start. Legacy candidate polling remains in the tree until it is removed; it is not how the app is run.

**Primary docs:** [README.md](README.md) for setup, configuration, and usage. [docs/CONFIG.md](docs/CONFIG.md) for workflow configuration. [docs/adr/](docs/adr/) for accepted decisions. [docs/plans/](docs/plans/) for product plans.

## Repository map

- `src/Symphony/`: web host entry point. MVC controllers and views, plus agent execution, config, domain models, issue tracker integration, orchestration, and workspace management. Orchestration polling is not registered at startup.
- `tests/Symphony.Tests/`: NUnit test suite. This is the only test project for default verification and CI.
- `tests/Symphony.CodingAgentsProbe/`: manual Copilot SDK probes (hosted Copilot and BYOM). Not part of CI.
- `tests/Symphony.PerformanceTests/`: manual performance harness. Not part of CI.
- `docs/adr/`: architecture decision records. Read the folder before revisiting an accepted decision.
- `docs/plans/`: implementation plans (web product, GitHub Issues tracker, task breakdown).
- `Symphony.slnx`: solution entry point.

## Build & Verify

```bash
dotnet build Symphony.slnx
dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"
```

Do not run `dotnet test` without that filter. Integration tests, including `DapploJiraTests`, need `JIRA_URL`, `JIRA_USER`, and `JIRA_TOKEN`. Without those variables they fail immediately with `ArgumentNullException` on `uriString`. That failure is expected. Do not investigate it, change those tests, or invent credentials unless the human explicitly asked to run Integration tests.

CI is `.github/workflows/ci.yml`. It builds and tests only `tests/Symphony.Tests` with the same filter. Probes and performance tests are opt-in and are not CI:

```bash
dotnet run --project tests/Symphony.CodingAgentsProbe -- copilot "Say hello in one sentence."
dotnet run -c Release --project tests/Symphony.PerformanceTests
```

## Code Style

- `.editorconfig` at repo root is authoritative; follow it over any convention below.
- **Private fields:** `camelCase` without `_` prefix (`logger`, not `_logger`).
- **Member ordering:** inner classes → constructors → properties → methods; static before instance; private → protected → internal → public.
- **Primary constructors:** preferred for service/DI classes over classic constructor with field assignment.
- **Sealed by default:** use `sealed class` for non-abstract classes unless inheritance is explicitly designed.
- **Collection expressions:** `[]` for empty/static collections; `new List<T>()` or `new Dictionary<K,V>()` for mutable ones.
- **Nullable reference types:** enabled; do not introduce avoidable warnings.
- **Omit braces** from single-line `if`/`else` bodies when the body fits one line and is on the same line as the condition.
- **No comments** in generated code unless explaining a non-obvious design decision.
- **No Hungarian notation** — no prefixes encoding scope or mutability.

## Testing Conventions

- **Framework:** NUnit 4.x — `[Test]`, `Assert.That(...)`, `Assert.ThrowsAsync`, no `[TestCase]`
- **Naming:** `Method_Scenario_ExpectedBehavior` PascalCase
- **Pattern:** Arrange-Act-Assert (AAA); no explicit comments needed
- **Organization:** one test class per source class, `*Tests.cs` suffix; split by behavior when a class is large
- **Mocking:** prefer real implementations where feasible; use `Substitute.For<T>()` only when external dependencies require it
- **Scope:** `tests/Symphony.Tests` is unit tests. Integration tests there are an emergency only, and must stay in-process. No browser, Docker, or external network. See [ADR-0002](docs/adr/0002-test-boundaries-and-playwright.md).
- **Playwright:** always through the Playwright MCP, against a host launched with `dotnet run`. Do not add Playwright or web smoke tests to `Symphony.Tests`. Later browser scripts, if needed, are JavaScript or TypeScript under `tests/`, not in `Symphony.Tests`.
- **Default run:** the CI category filter above. Never run Integration tests to "see if the suite is green."
