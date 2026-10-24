# T19: Secret protection service

## Problem
Field-level encryption/decryption for secrets needs a dedicated service using ASP.NET Core Data Protection.
Redaction helpers currently only support strings; event payloads and status DTOs are not sanitized.

## Proposed changes

### `src/Symphony/Security/SecretProtector.cs`
- Expose purpose strings (`Symphony`, `Secrets`, `v1`) and use them consistently.
- Keep `Encrypt`/`Decrypt` aliases.
- Add test-friendly constructors:
  - `SecretProtector(string keyRingPath)`
  - `SecretProtector(DirectoryInfo keyRingDirectory)`
- Make `AddSymphonySecretProtection` self-contained: register Data Protection if not already registered (using `configuration`/`contentRoot`), then resolve `IDataProtectionProvider`.

### `src/Symphony/Security/Redaction.cs`
- Keep `Redacted = "***"`, `Redact`, `RedactSecrets` string behavior.
- Add `IsSecretName` heuristic.
- Add generic `RedactSecrets<T>(T?)` using JSON clone-and-sanitize for serializable objects, best-effort mutation for POCOs.
- Add aliases `Redact<T>`, `RedactObject`, `RedactObject<T>`, `RedactPayload`, `RedactEventPayload`.
- Add remove-keys helpers `RemoveSecrets<T>/StripSecrets` for dictionaries/event payloads.

## Blast radius
- Consumers: ConfigurationService, ConfigurationEndpoints, AccountController, DataProtection registration.
- Tests: SecretProtectorTests, UserConfigurationPersistenceTests.

## Verification
- `dotnet build Symphony.slnx`
- `dotnet test tests/Symphony.Tests --filter "Category!=Integration&Category!=Smoke&Category!=ReleaseReadiness"` (ask human first)

## Planned commits
1. docs: plan T19 in TODO.md
2. feat: add key-ring constructors and self-contained secret protection registration
3. feat: add object/event/status redaction helpers
4. test: extend SecretProtector and redaction coverage
5. docs: remove TODO.md — plan executed

## GitHub issues log
- (none yet)
```