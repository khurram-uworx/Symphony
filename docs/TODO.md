# T25: IServiceConfigProvider refactor

## Problem
Runtime config is obtained from the concrete `ServiceConfigProvider`, which depends on
`WorkflowManager` and the global workflow state. This makes `AgentRunner` and
`WorkspaceManager` difficult to test and prevents per-job runtime configuration
from being injected.

## Proposed changes
- Introduce `IServiceConfigProvider` with `ServiceConfig GetConfig()`.
- Make `ServiceConfigProvider` implement it.
- Add `StaticServiceConfigProvider` for per-job/runtime static configs.
- Update `AgentRunner` and `WorkspaceManager` to depend on `IServiceConfigProvider`.
- Add `InternalsVisibleTo("Symphony.Tests")` for test access.
- Base implementation details: no run-algorithm changes; workspace traversal safety unchanged.

## Verification steps
- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"`

## Planned commits
1. docs: plan T25 in TODO.md
2. feat: introduce IServiceConfigProvider and StaticServiceConfigProvider
3. refactor: use IServiceConfigProvider in AgentRunner and WorkspaceManager
4. chore: expose internals to Symphony.Tests
5. test: (as needed) verify injection

## Blast radius
- `src/Symphony/Config/ServiceConfigProvider.cs`: class implements new interface.
- `src/Symphony/Agent/AgentRunner.cs`: constructor and field type.
- `src/Symphony/Workspaces/WorkspaceManager.cs`: constructor and field type.
- `src/Symphony/Symphony.csproj`: InternalsVisibleTo.
- Downstream callers: `OrchestratorService` still constructs concrete provider and is unchanged.

## Grounding
- microsoft-learn: dependency injection interface patterns.
- code-memory: symbol/relationship navigation.

## GitHub issues log
- (none yet)
'