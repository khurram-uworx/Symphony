# Test Plan — Make tracker optional and remove polling path

## Build
- [x] `dotnet build Symphony.slnx`

## Unit / non-integration tests
- [x] `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` — 60 passed, 0 failed

## Manual smoke tests
1. **No tracker, inline work only**
   - Start the host: `dotnet run --project src/Symphony`
   - In `/account/configuration`, set Work source to `None (inline work only)`, save.
   - In `/account/run`, submit work items only (no ticket IDs), with a GitHub repo/PAT and a workflow. Expect `202` with a job ID.
   - Open the job status; expect the job to complete without tracker-related errors.

2. **Ticket-only with no tracker → 400**
   - With Work source still `None`, submit only ticket IDs. Expect `400` with a message indicating no tracker is configured.

3. **Mixed submission with no tracker**
   - Submit work items + ticket IDs with no tracker configured. Expect the job to queue, inline items to run, and ticket rows to be recorded as failed with "No tracker configured; submit work items or configure a tracker."

4. **Tracker configured (Linear/Jira shape)**
   - Set Work source to Linear/Jira, fill endpoint/key/slug. Existing tracker-backed flows should still resolve ticket IDs (no polling reintroduced). Inline work still works.

5. **UI**
   - Configuration page shows "Work source" with `None (inline work only)` selected by default and hides tracker fields when none. Run page shows a hint that ticket IDs require a configured tracker.

## Notes
- `tests/Symphony.Tests` excludes `DapploJiraTests` (needs JIRA credentials) by category as per repo guidance.
- Candidate/terminal polling paths are removed; `IIssueTracker` now only supports `FetchIssueStatesByIdsAsync`.
- `NullIssueTracker` no longer fabricates issues for unknown IDs.
