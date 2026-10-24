# T27: Coding agent factory (Copilot only)

## Problem
No factory exists to create coding agents from a ServiceConfig. Copilot BYOM settings
(provider type, base URL, model ID, secret, optional OpenAI wire API) are parsed into
CopilotConfig but never reach CopilotAgent.InitializeAsync. Codex runtime support is
out of scope; codex front matter should not flow into effective config.

## Planned changes
1. Add src/Symphony/Agent/CodingAgentFactory.cs: static Create(ServiceConfig) returning ICodingAgent, validating supported Copilot provider types (openai, azure, anthropic).
2. Update CopilotAgent to apply ServiceConfig.Copilot BYOM settings into SessionConfig (Model, Provider).
3. Update ServiceConfigProvider.GetConfig() to read CopilotConfig from workflow config.
4. Update ServiceConfigProvider.ValidateForDispatch() to reject unsupported Copilot provider types.
5. Update WorkflowLoader.Parse() to ignore Codex-related front matter keys.

## Verification
- dotnet build Symphony.slnx
- dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness" (ask before running)

## GitHub issues log
- none yet
