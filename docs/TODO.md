# Plan: T13 Configuration API (issue #37)

## Problem
Expose `GET /api/configuration` and `PUT /api/configuration` for the current authenticated user.
Return and update the user's `UserConfiguration`, mask secret fields in responses, validate updates,
enforce ownership, and support partial secret updates (leave blank/masked to keep).

## Proposed changes
- Create `src/Symphony/Dto/ConfigurationDtos.cs` with `ConfigurationDto` and alias names.
- Create `src/Symphony/Api/ConfigurationEndpoints.cs` with minimal API endpoints.
- Update `Program.cs` to register `ConfigurationService` with `ISecretProtector` and map endpoints.

## Verification
- `dotnet build Symphony.slnx`
- Targeted tests if present.

## Planned commits
1. docs: plan T13 in TODO.md
2. feat: add configuration DTOs
3. feat: add configuration API endpoints
4. feat: register configuration endpoints in Program.cs

## Blast radius
- New Api/Dto namespaces.
- Program.cs DI and endpoint mapping.
- ConfigurationService unchanged except DI wiring.

## GitHub issues log
- (none yet)