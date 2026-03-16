# Symphony.PerformanceTests

Performance and allocation benchmark harness for Symphony.

This project is **not** part of the default test suite and is not run in CI. It is a
manual, opt-in console app for throughput, allocation, and memory A/B comparisons.

## Build & Run

```bash
dotnet run -c Release --project tests/Symphony.PerformanceTests
```

Always benchmark in `-c Release`.

## When to add a scenario

- Add a scenario row for repeatable operations, or a standalone `--<mode>` flag for one-off A/B comparisons.
- Document the scenario, its results, and methodology here.
- Keep scenarios focused and reproducible.