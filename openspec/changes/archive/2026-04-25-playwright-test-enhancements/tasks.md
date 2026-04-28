## 1. IntegrationTestBase Enhancement

- [x] 1.1 Add protected properties for UserId and UserName in IntegrationTestBase
- [x] 1.2 Implement user creation via POST /users API in InitializeAsync
- [x] 1.3 Implement user deletion via DELETE /users/{id} API in DisposeAsync
- [x] 1.4 Set X-MyMusic-UserName header for API request context

## 2. TestUserScope Conversion

- [x] 2.1 Convert TestUserScope to a Playwright test class extending PageTest
- [x] 2.2 Add test method that navigates to application URL
- [x] 2.3 Add assertion to verify username appears in GUI topbar
- [x] 2.4 Implement proper user cleanup in DisposeAsync

## 3. Documentation Updates

- [x] 3.1 Add integration test rules section to AGENTS.md (succinct, 2-3 lines)
- [x] 3.2 Add test running commands to AGENTS.md (dotnet test, dotnet test --filter)
- [x] 3.3 Add detailed integration test guidelines to docs/development/server.md
- [x] 3.4 Add integration test guidelines to docs/development/client.md

## 4. Verification

- [x] 4.1 Build project: `dotnet build`
- [x] 4.2 Run integration tests: `dotnet test MyMusic.IntegrationTests`
- [x] 4.3 Verify test user is created and deleted correctly
- [ ] 4.4 Verify GUI test passes with username in topbar (requires browser auth integration)
