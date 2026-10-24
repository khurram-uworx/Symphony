# Plan: T28 Workflow front matter overrides

## Problem
Workflow authoring should not be able to replace tracker or other run-level configuration. Only a small set of runtime knobs may be overridden by workflow front matter: workspace root, hooks, max turns, and per-state concurrency. Malformed front matter must fail clearly and atomically.

## Proposed changes
- WorkflowLoader: parse YAML front matter safely; strip Codex keys; throw clear `WorkflowException` on malformed/unclosed front matter.
- ServiceConfigFactory: keep applying only allowed overrides; keep tracker from user config.
- ConfigReader: retain typed readers.
- Documentation: list allowed override keys and explicitly immutable settings.

## Verification steps
- `dotnet build Symphony.slnx`
- Default unit test filter
- Focused checks for malformed YAML, Codex stripping, override application, tracker immutability.

## Planned commits
1. docs: plan T28 in TODO.md
2. docs: document allowed override keys and validation behavior
3. feat: ensure workflow front matter overrides are atomic and validated
4. test: cover workflow front matter override boundaries

## Blast radius
- `src/Symphony/Workflows/WorkflowLoader.cs`
- `src/Symphony/Config/ServiceConfigFactory.cs`
- `src/Symphony/Config/ServiceConfigProvider.cs`
- `docs/CONFIG.md`

## GitHub issues log

- [ ] No new issues created yet.