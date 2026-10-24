# Plan: Milestone 6 - API Keys, curl submission, and M6 verification (T39-T43)

## Problem
Enable terminal `curl` submission against arbitrary configured repository/ticket by adding user-owned API keys with secure lifecycle management, exposing a curl-oriented launcher, hardening APIs, and verifying end-to-end curl flow.

## Goals (from SYMPHONY-WEB-PLAN.md)
- T39: API key management (create/rotate/enable-disable/revoke/list). Store hashed keys only; show raw key once. Prefix lookup + constant-time verification. Endpoints under `/api/api-keys`, page at `/account/api-keys`. Middleware attributes requests to owner. Invalidate on user disable/delete. Admins do not manage other users' keys.
- T40: API key tests (lifecycle, rotation/revoke, expiry, hash verification, ownership, middleware).
- T41: curl-oriented run page (`/account/run`) API tab shows live curl example using enabled API key (prompt if none). Placeholder tokens, never embed real secrets.
- T42: API validation and rate limiting for API endpoints; consistent errors; reject malformed work items/repos early.
- T43: Arbitrary repo/ticket curl verification (document + test with API key; verify 202/status URL/terminal state).

## Decisions (confirmed)
- UI must have option to enable/disable and rotate the key; key is always system-generated.
- When admin disables a user, user cannot logon and their API keys are also considered disabled (cannot use system via automation). This is already specified (T9/T39 cascade).

## Blast radius
- Domain/Data: `src/Symphony/Domain/ApiKey.cs` (new), `src/Symphony/Data/SymphonyDbContext.cs` (DbSet + mapping/index), migrations.
- Security/Middleware: `src/Symphony/Middleware/ApiKeyMiddleware.cs` (new), possibly auth integration to treat disabled user/disabled key as unauthorized.
- API: `src/Symphony/Api/ApiKeyEndpoints.cs` (new), updates to auth to accept API keys for API routes; ensure `POST /api/work` can be called with API key.
- Services: API key service (new) for generation/hash/verification/rotation/invalidation.
- UI: `src/Symphony/Controllers/AccountController.cs` (API keys page actions), `src/Symphony/Views/Account/ApiKeys.cshtml` (new), `src/Symphony/Views/Account/Run.cshtml` (update for live curl example).
- Validation/Rate limiting: `src/Symphony/Program.cs` (middleware/policies), DTO validation.
- Tests: `tests/Symphony.Tests/ApiKeyServiceTests.cs`, `tests/Symphony.Tests/ApiKeyMiddlewareTests.cs` (new).
- Config/Docs: may update docs as needed for curl usage.

## Grounding plan (G1)
- Ground API key patterns (secure random, prefix+hash, rotation) against Microsoft docs if needed via microsoft-learn MCP.
- Navigate codebase with code-memory MCP to understand existing auth/cookie/API patterns, middleware, endpoint registration.
- Assess blast radius above.

## Proposed changes (high level)
1. **ApiKey entity + schema**
   - `ApiKey` with Id, OwnerUserId, Name/Description, KeyPrefix, KeyHash, IsEnabled, ExpiresAt (nullable), RevokedAt (nullable), CreatedAt, LastUsedAt, RotatedFromId (nullable).
   - Add index on (OwnerUserId). Add mapping in DbContext.
   - Migration.

2. **API key service**
   - Generate secure random key, compute prefix, hash with secure hasher; use constant-time comparison.
   - Create: returns (rawKey once, metadata).
   - Rotate: create new key, revoke/disable old or mark rotated; return new raw key once.
   - Enable/disable, revoke, list (metadata only).
   - Verify: find by prefix, compare hash constant-time, check enabled/not expired/not revoked, update LastUsedAt.

3. **Middleware for API key auth**
   - Check `Authorization: Bearer <key>` or `X-API-Key` header on API routes. Resolve user/owner, set claims/principal. Treat disabled user or disabled/revoked/expired key as 401.

4. **API endpoints**
   - `GET /api/api-keys`, `POST /api/api-keys`, `POST /api/api-keys/{id}/rotate`, `PATCH /api/api-keys/{id}`, `DELETE /api/api-keys/{id}`.

5. **UI**
   - `/account/api-keys` page to list, create (show raw key once), rotate, enable/disable, revoke/delete.
   - `/account/run` API tab: show curl example using enabled API key (or placeholder if none).

6. **Validation & rate limiting**
   - Add request validation for API DTOs. Add rate limiting policies for API endpoints via ASP.NET Core rate limiter.

7. **Tests**
   - ApiKeyServiceTests and ApiKeyMiddlewareTests.

8. **Verification**
   - T43: test curl with API key against local app.

## Verification steps
- [ ] Build: `dotnet build Symphony.slnx`
- [ ] Tests (unit): `dotnet test tests/Symphony.Tests`
- [ ] Manual: create API key, call `POST /api/work` with API key -> job queued
- [ ] Manual: disable user as admin -> API key auth fails
- [ ] Manual: rotate key -> old fails, new works
- [ ] Manual: enable/disable key -> toggles access

## Planned commits
1. `feat(api-keys): add ApiKey entity, DbContext mapping, migration`
2. `feat(api-keys): add API key service with secure generation, hashing, rotation, lifecycle`
3. `feat(api-keys): add API key auth middleware and integrate into pipeline`
4. `feat(api-keys): add API endpoints for key management (/api/api-keys)`
5. `feat(api-keys): add account API keys UI (list/create/rotate/enable-disable)`
6. `feat(api-keys): update /account/run with live curl example using API key`
7. `feat(api): add request validation and rate limiting for API endpoints`
8. `test(api-keys): add ApiKeyServiceTests and ApiKeyMiddlewareTests`

## GitHub issues log
- [ ] #TBD — Track any follow-ups discovered during implementation

## Notes
- Store hashed keys only; raw key shown once. Use prefix lookup + constant-time verification.
- Invalidate keys on user disable/delete. Admins do not manage other users' keys.
