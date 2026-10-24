# AGENTS.md

## Project overview

Symphony is a .NET 10 web host for AI agent orchestration. The entry point is the ASP.NET Core MVC app in `src/Symphony`. It serves HTTP and does not require a workflow file to start.

**Primary docs:** [README.md](README.md) for setup, configuration, and usage. [CONTAINER-README.md](CONTAINER-README.md) for the published image and sample compose. [docs/CONFIG.md](docs/CONFIG.md) for workflow configuration. [docs/adr/](docs/adr/) for accepted decisions. [docs/plans/](docs/plans/) for product plans.

## Repository map

- `src/Symphony/`: web host entry point. MVC controllers and views, plus agent execution, config, domain models, issue tracker integration, orchestration, and workspace management.
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

CI is `.github/workflows/ci.yml`. It builds and tests only `tests/Symphony.Tests` with the same filter and collects Cobertura coverage via `coverlet.collector`; the job fails if tests run but no coverage file is produced. Probes and performance tests are opt-in and are not CI:

```bash
dotnet run --project tests/Symphony.CodingAgentsProbe -- copilot "Say hello in one sentence."
dotnet run -c Release --project tests/Symphony.PerformanceTests
```

Local web host (deterministic port): `dotnet run --project src/Symphony` listens on `http://localhost:5080` by default via `src/Symphony/Properties/launchSettings.json`. Override with `ASPNETCORE_URLS=http://localhost:<port>` (or `https://...`) when needed.

Smoke checks: delete `src/Symphony/App_Data/symphony.db` to force a fresh database and seed credentials, launch the host, then use the configured Playwright MCP against `http://localhost:5080`. Read the seeded admin username/password from host stdout. Keep these checks out of `Symphony.Tests`.

Release publishing is `.github/workflows/cd.yml`: it builds and pushes `ghcr.io/${{ github.repository_owner }}/symphony` (version tag + `latest`) on a `v*` tag or manual dispatch. `.github/workflows/docker.yml` is the manual publish entry point. `cd.yml` is the publish workflow, not a build-only check.

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

## UI Design

- Look at the existing pages, CSS components, spacing, typography, and component conventions before adding or changing UI. Keep new work visually consistent with the surrounding product rather than inventing one-off styles.
- Designs should be sleek and modern: consistent spacing, alignment, color tokens, affordances, and interactions. Avoid bare-bone 90s-style HTML inputs/buttons/forms, loose alignment, ambiguous actions, or ad hoc styling.
- Prefer shared design patterns and existing CSS classes/variables over inline styles or isolated component markup. When adding a new visual element, place it within the established layout and visual hierarchy instead of appending it awkwardly.
- Destructive actions should be clearly identifiable as dangerous and should not be visually inconsistent with primary/secondary actions.

## Testing Conventions

- **Framework:** NUnit 4.x — `[Test]`, `Assert.That(...)`, `Assert.ThrowsAsync`, no `[TestCase]`
- **Naming:** `Method_Scenario_ExpectedBehavior` PascalCase
- **Pattern:** Arrange-Act-Assert (AAA); no explicit comments needed
- **Organization:** one test class per source class, `*Tests.cs` suffix; split by behavior when a class is large
- **Mocking:** prefer real implementations where feasible; use `Substitute.For<T>()` only when external dependencies require it
- **Scope:** `tests/Symphony.Tests` is unit tests. Integration tests there are an emergency only, and must stay in-process. No browser, Docker, or external network. See [ADR-0002](docs/adr/0002-test-boundaries-and-playwright.md).
- **Playwright:** always through the Playwright MCP, against a host launched with `dotnet run`. Do not add Playwright or web smoke tests to `Symphony.Tests`. Later browser scripts, if needed, are JavaScript or TypeScript under `tests/`, not in `Symphony.Tests`.
- **Default run:** the CI category filter above. Never run Integration tests to "see if the suite is green."
- **Coverage style:** keep `Symphony.Tests` lean and fast. Prefer one focused test that asserts multiple related behaviors over several redundant tests, and test acceptance criteria rather than private helpers. Do not add tests whose only value is coverage.
