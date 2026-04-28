## Why

Integration tests currently create test users via the REST API but have no song data. Each test needs to set up its own test data, leading to duplicated code and inconsistent test scenarios. By creating reusable fixtures that seed realistic song data from the production API, tests become simpler, more consistent, and can focus on behavior rather than data setup.

## What Changes

- Create fixture classes in `MyMusic.IntegrationTests/Fixtures/` that seed test data via public REST APIs
- Each fixture has a `Data` property with a `List<T>` of data models representing the seeded entities
- Fixtures use the existing API client (`IAPIRequestContext`) from `IntegrationTestBase` to insert data
- Fixtures for entities without public CREATE APIs will require backend endpoints to be added first

### Fixture Classes

| Fixture | Data Property | API Used | Notes |
|---------|---------------|----------|-------|
| `UsersFixture` | `List<UserData>` | `POST /api/users` | Already exists in IntegrationTestBase |
| `DevicesFixture` | `List<DeviceData>` | `POST /api/devices` | Direct CRUD available |
| `PlaylistsFixture` | `List<PlaylistData>` | `POST /api/playlists` | Direct CRUD available |
| `SongsFixture` | `List<SongData>` | `POST /api/songs/upload` | Requires file simulation |
| `ArtistsFixture` | `List<ArtistData>` | N/A | Created via song import |
| `AlbumsFixture` | `List<AlbumData>` | N/A | Created via song import |
| `GenresFixture` | `List<GenreData>` | N/A | Created via song import |

### Data Models

Each fixture uses a simple data model (record or class) with the fields needed to create the entity via API:

```csharp
public record DeviceData(string Name, string Icon, string? Color = null, string? NamingTemplate = null);
public record PlaylistData(string Name, List<long>? SongIds = null);
public record SongData(string Title, string FilePath, DateTime ModifiedAt, DateTime CreatedAt);
```

### Phased Implementation

**Phase 1: Entities with existing APIs**
- DevicesFixture (POST /api/devices available)
- PlaylistsFixture (POST /api/playlists available)

**Phase 2: Song upload support**
- Create minimal test audio file for upload simulation
- SongsFixture (POST /api/songs/upload available but needs file)

**Phase 3: Backend endpoints needed**
- ArtistsFixture → Requires `POST /api/artists` endpoint
- AlbumsFixture → Requires `POST /api/albums` endpoint
- GenresFixture → Requires `POST /api/genres` endpoint

## Capabilities

### New Capabilities
- `integration-test-fixtures`: Reusable test data fixtures that seed the database via REST API for integration testing

### Modified Capabilities
- None

## Impact

- **IntegrationTests**: New `Fixtures/` directory with fixture classes for devices, playlists, songs
- **Server**: May need new POST endpoints for artists, albums, genres (Phase 3)
- **Tests**: Can use fixtures to quickly set up test data, reducing boilerplate

## Non-Goals

- Creating fixtures for entities that are only created through import (e.g., Sources)
- Supporting complex song metadata relationships in fixtures
- Performance optimization for large datasets
