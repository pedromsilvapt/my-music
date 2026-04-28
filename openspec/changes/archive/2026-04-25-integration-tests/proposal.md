## Why

The project lacks integration testing infrastructure. Unit tests with SQLite in-memory databases verify business logic but cannot test the full stack (HTTP server, authentication headers, database migrations, file system operations). We need Playwright-based integration tests that exercise real HTTP endpoints against a running server with PostgreSQL.

Additionally, integration tests require isolated test users that can be created and deleted cleanly. Currently there's no API to delete a user and their associated data (songs, playlists, files).

## What Changes

- New `MyMusic.IntegrationTests` project using Playwright for .NET with xUnit
- New Caddy route on port 5001 for integration testing (no header injection, tests set `X-MyMusic-UserName` directly)
- New `DELETE /users/{id}` endpoint to delete a user with cascade cleanup
- Add `MYMUSIC_HEADER_ID_ENABLED=1` to server launchSettings for header-based auth

## Capabilities

### New Capabilities

- `user-deletion`: API endpoint to delete a user by ID, including all owned entities (songs, albums, artists, playlists, devices, etc.) and their music repository files

### Modified Capabilities

- None

## Impact

- **New project**: `MyMusic.IntegrationTests/` with Playwright infrastructure
- **Modified**: `compose.yaml` - add Caddy route on port 5001
- **Modified**: `MyMusic.Server/Properties/launchSettings.json` - add `MYMUSIC_HEADER_ID_ENABLED` env var
- **New service**: `UserDeleteService` implementing `IUserDeleteService`
- **New controller endpoint**: `DELETE /users/{id}` in `UsersController`
- **New DTOs**: Delete user request/response in `DTO/Users/`
- **File system**: Music repository files under `{MusicRepositoryPath}/{username}/` will be deleted when user is deleted
