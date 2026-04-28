## Why

The existing integration test infrastructure (IntegrationTestBase, TestUserScope) was designed for API-only testing. It needs enhancement to support full Playwright browser tests that verify end-user functionality through the GUI. Additionally, test organization and documentation need clarification to ensure proper separation between unit tests (business logic) and integration tests (web page interactions).

## What Changes

- IntegrationTestBase will create and delete test users via API in InitializeAsync/DisposeAsync
- TestUserScope converted to a Playwright test class that verifies username in GUI topbar
- Integration tests focus on end-user functionality via Playwright browser interactions
- Business logic tests remain in MyMusic.Common.Tests (unit tests)
- Documentation added to AGENTS.md (succinct) and server.md/client.md (detailed)
- Test running instructions added to AGENTS.md

## Capabilities

### New Capabilities

- `playwright-gui-tests`: Playwright-based integration tests that interact with web pages, verify GUI elements, and test end-user workflows

### Modified Capabilities

- None

## Impact

- **Modified**: `MyMusic.IntegrationTests/IntegrationTestBase.cs` - add user creation/deletion via API
- **Modified**: `MyMusic.IntegrationTests/TestUserScope.cs` - convert to Playwright test class with GUI verification
- **Modified**: `AGENTS.md` - add integration test rules and test running instructions
- **Modified**: `docs/development/server.md` - add detailed integration test guidelines
- **Modified**: `docs/development/client.md` - add detailed integration test guidelines
