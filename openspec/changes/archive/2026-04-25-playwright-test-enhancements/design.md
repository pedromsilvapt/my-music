## Context

The project has integration tests in `MyMusic.IntegrationTests` using Playwright with xUnit. The current infrastructure (`IntegrationTestBase`, `TestUserScope`) was designed for API-only testing. We need to enhance it for full Playwright browser tests that verify end-user functionality through the GUI.

Current state:
- `IntegrationTestBase` creates `IAPIRequestContext` with auth headers but doesn't create/delete users
- `TestUserScope` is a helper class that creates/deletes users via API, used manually in tests
- No browser-based tests exist yet
- Documentation doesn't clarify test separation (unit vs integration)

## Goals / Non-Goals

**Goals:**
- IntegrationTestBase handles test user lifecycle (create on init, delete on dispose)
- All integration tests inherit from IntegrationTestBase
- TestUserScope becomes a Playwright test class that verifies username in GUI topbar
- Integration tests focus on end-user functionality via Playwright browser interactions
- Business logic tests stay in MyMusic.Common.Tests
- Clear documentation for test organization and running instructions

**Non-Goals:**
- Migrating existing unit tests
- Creating comprehensive test coverage (infrastructure only)
- CI/CD pipeline integration

## Decisions

### D1: IntegrationTestBase User Lifecycle

**Decision:** IntegrationTestBase will create a test user in `InitializeAsync()` and delete it in `DisposeAsync()`.

**Rationale:** Tests need isolated users. Creating/deleting in the base class ensures every test has a clean user automatically, reducing boilerplate.

**Implementation:**
- Call `POST /users` in InitializeAsync to create test user
- Store UserId and UserName in protected properties
- Call `DELETE /users/{id}` in DisposeAsync
- Set `X-MyMusic-UserName` header for API requests

### D2: TestUserScope → Playwright Test Class

**Decision:** Convert TestUserScope to a test class that extends PageTest and verifies the username appears in the GUI topbar.

**Rationale:** The TestUserScope pattern was a helper for API tests. For browser tests, we need a test class that:
1. Creates a test user via API
2. Navigates to the app
3. Verifies the username is displayed in the topbar

**Implementation:**
- Rename to `TestUserDisplayTests` (or similar)
- Extend `PageTest` from Playwright.Xunit
- Create user via APIRequestContext
- Navigate to app URL (from config)
- Assert username visible in topbar selector
- Delete user in DisposeAsync

### D3: Test Organization Rules

**Decision:** Establish clear separation between test types.

| Test Type | Location | Purpose |
|-----------|----------|---------|
| Unit Tests | MyMusic.Common.Tests | Business logic, services, algorithms |
| Integration Tests | MyMusic.IntegrationTests | Playwright browser tests, end-user functionality |

**Rationale:** Clear boundaries prevent confusion. Unit tests test implementation details (services, methods). Integration tests test user-visible behavior (pages, workflows).

### D4: Documentation Updates

**Decision:** Add test guidelines to documentation.

- **AGENTS.md**: Succinct section on test types and running commands
- **docs/development/server.md**: Detailed integration test patterns and examples
- **docs/development/client.md**: How Playwright tests interact with React components

**Rationale:** Developers need clear guidance on where to write tests and how to run them.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Browser tests slower than API tests | Keep integration tests focused on critical user paths |
| Playwright browser installation required | Document installation steps clearly |
| Test isolation depends on shared dev database | Use unique usernames with GUIDs |
| GUI selectors may break on UI changes | Use stable selectors (data-testid attributes) |
