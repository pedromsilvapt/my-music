## 1. Server Configuration

- [x] 1.1 Add `MYMUSIC_HEADER_ID_ENABLED=1` environment variable to `MyMusic.Server/Properties/launchSettings.json` http profile
- [x] 1.2 Add Caddy route on port 5001 in `compose.yaml` that proxies `/api/*` to `localhost:5000` without header injection

## 2. User Deletion Service

- [x] 2.1 Create `IUserDeleteService` interface in `MyMusic.Common/Services/`
- [x] 2.2 Create `UserDeleteService` implementation in `MyMusic.Server/Services/` with:
  - Clear user's `CurrentQueueId` (handle circular reference)
  - Delete user entity (EF Core cascade handles owned entities)
  - Delete user's music repository directory `{MusicRepositoryPath}/{username}/`
- [x] 2.3 Register `IUserDeleteService` in DI in `MyMusic.Server/HostBuilderExtensions.cs`

## 3. User Deletion API

- [x] 3.1 Create `DeleteUserRequest` DTO in `MyMusic.Server/DTO/Users/`
- [x] 3.2 Create `DeleteUserResponse` DTO in `MyMusic.Server/DTO/Users/`
- [x] 3.3 Add `DELETE /users/{id}` endpoint in `UsersController` that:
  - Returns 404 if user not found
  - Calls `IUserDeleteService.DeleteAsync(id)`
  - Returns 200 OK with deleted user info

## 4. Integration Test Project

- [x] 4.1 Create `MyMusic.IntegrationTests` project with:
  - `Microsoft.Playwright.Xunit` package
  - `xunit` and `Shouldly` packages
  - ProjectReference to `MyMusic.Server`
- [x] 4.2 Create `IntegrationTestBase.cs` that:
  - Extends `PlaywrightTest` from Playwright.Xunit
  - Creates `IAPIRequestContext` with `BaseURL = "http://localhost:5001/api"`
  - Sets `ExtraHTTPHeaders` with `X-MyMusic-UserName: Test-{GUID}`
  - Disposes context in cleanup
- [x] 4.3 Create `TestUserScope.cs` helper that:
  - Creates a test user via `POST /users` in constructor
  - Deletes test user via `DELETE /users/{id}` in `Dispose()`
- [x] 4.4 Build project and install Playwright browsers: `pwsh bin/Debug/net10.0/playwright.ps1 install`

## 5. Verification

- [x] 5.1 Run `dotnet build` to verify all projects compile
- [x] 5.2 Run `dotnet test MyMusic.IntegrationTests` to verify test infrastructure works (no actual tests yet)
- [x] 5.3 Verify `DELETE /users/{id}` works via manual API call or test
