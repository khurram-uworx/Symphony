# Contributing

Contributions are welcome! Please ensure:
- Code builds without warnings
- Tests pass
- Code follows the existing style conventions

## Development Guidelines

- Follow the repository rules in [AGENTS.md](AGENTS.md).
- Keep changes consistent with the structure, naming, style, and patterns of the surrounding code.
- Use the [iterative-work](.agents/skills/iterative-work/SKILL.md) workflow for multi-step changes.
- Add tests in `tests/Symphony.Tests` following existing conventions.
- Do not add Playwright/web smoke tests to `Symphony.Tests` - use Playwright MCP against a launched host if needed.
- Run tests with the CI filter: `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`.
- Create GitHub issues for deferred work discovered during implementation.

## Code Style

- Follow `.editorconfig` at the repo root.
- Private fields: `camelCase` without `_` prefix.
- Member ordering: inner classes → constructors → properties → methods; static before instance; private → protected → internal → public.
- Prefer primary constructors for service/DI classes.
- Use `sealed class` by default unless inheritance is explicitly designed.
- Use collection expressions `[]` for empty/static collections.
- Nullable reference types enabled; avoid avoidable warnings.
- Omit braces from single-line `if`/`else` when on the same line.
- No comments in generated code unless explaining a non-obvious design decision.
- No Hungarian notation.

## Pull Requests

- Keep PRs focused and small where possible.
- Include a clear description of changes and testing performed.
- Ensure CI passes before requesting review.
- Do not push directly to `main`.
