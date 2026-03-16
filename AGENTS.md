# AGENTS.md

## Project overview

Symphony is a .NET 10 AI agent orchestration system that monitors issue trackers (Linear, Jira), creates isolated workspaces, and drives AI agents (Copilot, Codex) through workflow prompts until issues are resolved.

**Primary docs:** [README.md](README.md) for setup, configuration, and usage. [docs/CONFIG.md](docs/CONFIG.md) for workflow configuration. [docs/adr/](docs/adr/) for accepted decisions. [docs/plans/](docs/plans/) for product plans.

## Repository map

- `src/Symphony.App/`: main application — agent execution, config, domain models, issue tracker integration, orchestration, workspace management.
- `tests/Symphony.Tests/`: NUnit test suite. This is the only test project for default verification and CI.
- `tests/Symphony.CodingAgentsProbe/`: manual Copilot SDK probes (hosted Copilot and BYOM). Not part of CI.
- `tests/Symphony.PerformanceTests/`: manual performance harness. Not part of CI.
- `docs/adr/`: architecture decision records. Read the folder before revisiting an accepted decision.
- `docs/plans/`: implementation plans (web product, GitHub Issues tracker, task breakdown).
- `Symphony.slnx`: solution entry point.

## Build & Verify

```bash
dotnet build Symphony.slnx
dotnet test tests/Symphony.Tests
```

CI is `.github/workflows/ci.yml`. It builds and tests only `tests/Symphony.Tests`. Probes and performance tests are opt-in and are not CI:

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
