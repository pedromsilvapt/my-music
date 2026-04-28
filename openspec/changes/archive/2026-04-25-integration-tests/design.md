## Context

The project currently has unit tests using SQLite in-memory databases (`MyMusic.Common.Tests`) and CLI tests (`MyMusic.CLI.Tests`). Integration tests are needed to verify the full stack works correctly, including:

- HTTP authentication via `X-MyMusic-UserName` header
- PostgreSQL database with real migrations
- File system operations on music repository
- End-to-end request/response flows

The existing auth system uses `HttpCurrentUser` which resolves user identity from:
1. `MYMUSIC_USER_ID` environment variable (fallback)
2. `X-MyMusic-UserId` header (if `MYMUSIC_HEADER_ID_ENABLED=1`)
3. `X-MyMusic-UserName` header lookup (if `MYMUSIC_HEADER_ID_ENABLED=1`)

For tests to control authentication, we need a Caddy route that doesn't inject headers.

## Goals / Non-Goals

**Goals:**
- Create Playwright-based integration test infrastructure
- Enable test isolation via random test users
- Provide API to clean up test users completely (DB + files)
- Support header-based authentication for tests

**Non-Goals:**
- Full test coverage (just infrastructure for now)
- CI/CD pipeline integration (future work)
- Test database isolation (shared dev database)

## Decisions

### D1: Caddy Test Route on Port 5001

**Decision:** Add a new Caddy listener on port 5001 that proxies to the server without header injection.

**Rationale:** The existing route on port 80 injects `X-MyMusic-UserName: pedro`, overriding any test headers. A separate port allows tests to control auth while keeping dev workflow unchanged.

**Alternatives considered:**
- Conditional header in Caddy (only set if missing) - Caddy doesn't support this easily
- Direct connection to server:5000 - Bypasses production-like proxy setup
- Query parameter auth - Non-standard, security concerns

### D2: DELETE /users/{id} Endpoint

**Decision:** Add `DELETE /users/{id}` endpoint that deletes the specified user with full cleanup.

**Rationale:** Tests need to clean up after themselves. The endpoint accepts a path parameter `id` (not `/me`) to allow deleting specific test users. No admin check for now - suitable for dev/test environments.

**Cleanup scope:**
- Database: User entity (cascade deletes owned entities via EF Core)
- Files: `{MusicRepositoryPath}/{username}/` directory

**Alternatives considered:**
- DELETE /users/me - Only deletes current user, tests would need to track user ID
- Test-only endpoint on different route - Adds complexity
- Direct DB cleanup from tests - Bypasses API layer, misses file cleanup

### D3: UserDeleteService Pattern

**Decision:** Follow existing "one operation per service" pattern with `IUserDeleteService` in Common and `UserDeleteService` in Server.

**Rationale:** Consistent with existing patterns (`SongEditService`, `SongDeleteService`, etc.). Service handles:
1. Clear `User.CurrentQueueId` (circular reference)
2. Delete user entity (cascade handles DB cleanup)
3. Delete user's music repository directory

### D4: Playwright with xUnit

**Decision:** Use `Microsoft.Playwright.Xunit` package, following existing xUnit pattern in the project.

**Rationale:** Consistent with existing test projects. Playwright's xUnit integration provides:
- `PlaywrightTest` base class for API-only tests
- `PageTest` base class for browser tests
- Parallelizable test execution
- Built-in fixture management

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| DELETE /users/{id} without auth check | Document as dev/test-only; add auth check later if exposed publicly |
| Tests share dev database | Tests use unique usernames (`Test-{GUID}`); cleanup on teardown |
| File deletion irrecoverable | Service validates user exists before deletion; logs deletion |
| Playwright browser installation required | Document `pwsh bin/Debug/net10.0/playwright.ps1 install` in tasks |
