# ADR-0002: Test boundaries and Playwright

Status: Accepted

## Context

`tests/Symphony.Tests` is the only project default CI runs. It should stay a fast NUnit suite. Browser checks need a real host. Docker is not implemented, and the .NET SDK can launch the web host directly. Putting Playwright, `WebApplicationFactory` smoke tests, or a browser install into `Symphony.Tests` mixes a slow, environment-heavy check into the unit suite. The Playwright MCP is already available to agents.

Task T5 in `docs/plans/SYMPHONY-WEB-PLAN.md` suggested `WebApplicationFactory<Program>` smoke tests inside `Symphony.Tests`. That suggestion is superseded by this decision.

## Decision

- `tests/Symphony.Tests` is for unit tests. An integration test belongs there only in an emergency, when a unit test cannot reach the behavior and the dependency is already in-process. No browser, no Docker, and no external network.
- Do not add Playwright, browser tests, or web smoke tests to `Symphony.Tests`.
- Drive Playwright through the Playwright MCP. Launch the host with the .NET SDK (`dotnet run`), not Docker.
- For a live smoke check, point the host at a disposable SQLite file, read the one-time admin credentials from the host's first-start stdout, and delete that database when finished. A redirect from `/` to the login page is expected while the greeting page is authorized. It is not a startup failure.
- Later automated browser scripts, if needed, live under `tests/` as JavaScript or TypeScript Playwright tests. They do not live in `Symphony.Tests`.

## Consequences

- `dotnet test tests/Symphony.Tests` stays a unit suite, plus rare in-process integration tests.
- Browser smoke is an agent check through the Playwright MCP, not a NUnit category in `Symphony.Tests`.
- A future JS/TS Playwright project under `tests/` is allowed. It is not part of the default CI filter until a later task says so.
- Do not edit workflow files to force Playwright into CI as part of this decision.
