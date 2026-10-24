# T11: Data Protection persistence and cookie hardening

## Problem
Data Protection keys are not explicitly persisted to a shared/volume-backed location, auth cookie hardening (Secure/HttpOnly/SameSite/expiry/sliding) is not configured, and HTTPS redirection/HSTS are not enabled in production. This breaks multi-instance/container deployments and sign-in validity across restarts.

## Proposed changes
- Add `src/Symphony/Data/DataProtectionRegistration.cs` with `AddSymphonyDataProtection(...)` resolving a configurable keys path and calling `AddDataProtection().PersistKeysToFileSystem(...)`.
- Update `src/Symphony/Data/IdentityRegistration.cs` to harden application cookie options, with production detection for `Secure`.
- Update `src/Symphony/Program.cs` to register data protection, pass environment to identity, and enable HSTS/HTTPS redirection outside Development.
- Update `src/Symphony/appsettings.json` with a `DataProtection:KeysPath` default.
- Update compose files to set `DataProtection__KeysPath` to the mounted volume path.

## Verification
- `dotnet build Symphony.slnx`
- Targeted unit tests for identity/data protection registration (ask before running).
- Container build/run and Playwright smoke against PR.

## Planned commits
1. docs: plan T11 in TODO.md
2. feat: persist Data Protection keys to configured path
3. feat: harden Identity application cookie options
4. feat: enable HSTS and HTTPS redirection in production
5. test: cover registration behavior
6. docs: remove TODO.md — plan executed

## Blast radius
- `src/Symphony/Program.cs` startup pipeline
- `src/Symphony/Data/IdentityRegistration.cs`
- new `DataProtectionRegistration.cs`
- `appsettings.json`, compose files
- tests under `tests/Symphony.Tests/Data`

## GitHub issues log
- (none yet)
