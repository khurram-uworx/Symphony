# Symphony.CodingAgentsProbe

Manual probes for the GitHub Copilot SDK. Not part of the default test suite.

## copilot

GitHub-hosted Copilot, no custom provider.

```bash
dotnet run --project tests/Symphony.CodingAgentsProbe -- copilot "Say hello in one sentence."
```

## byom

OpenAI-compatible provider through `ProviderConfig.ApiKey`.

Verified configuration:

| Setting | Value |
| --- | --- |
| Provider type | `openai` |
| Base URL | `https://api.x.ai/v1` |
| Wire API | `completions` |
| Model | `grok-4.20-non-reasoning` |
| API key | `XAI_API_KEY` environment variable |

`responses` did not complete against this endpoint. `SendAndWaitAsync` does not return after a BYOM turn; the probe reads `AssistantMessageEvent` and completes on `SessionIdleEvent`.

```bash
dotnet run --project tests/Symphony.CodingAgentsProbe -- byom
```

Overrides: `byom [model] [baseUrl] [wireApi] [question...]`

## otel

Writes Copilot SDK spans to a JSONL file via `TelemetryConfig` (file exporter). Confirms the SDK's built-in OpenTelemetry support without an OTLP collector.

```bash
dotnet run --project tests/Symphony.CodingAgentsProbe -- otel
```

Optional output file override:

```bash
dotnet run --project tests/Symphony.CodingAgentsProbe -- otel ./copilot-otel.jsonl "Say hello in one sentence."
```
