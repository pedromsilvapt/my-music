## Context

The integration test project (`MyMusic.IntegrationTests`) uses Playwright for browser testing. Tests inherit from `IntegrationTestBase`, which creates a test user via `POST /api/users`. The test user is created with a unique username and cleaned up after each test.

Currently, tests have no way to seed song data, devices, or playlists. This forces each test to either:
1. Test with empty data (limited scenarios)
2. Create data manually via API calls (boilerplate)
3. Skip tests that require populated data

The real API at port 80 has ~150 songs with artists, albums, genres, and devices. We want to use this as source data for fixtures.

## Goals / Non-Goals

**Goals:**
- Create reusable fixture classes that seed data via public REST APIs
- Each fixture has a `Data` property exposing the seeded entity data
- Fixtures integrate cleanly with `IntegrationTestBase` lifecycle
- Data is fetched from the real API (port 80) for realistic test scenarios
- Phased implementation: start with entities that have existing APIs

**Non-Goals:**
- Complex fixture configuration or customization
- Performance optimization for seeding thousands of entities
- Fixtures for entities only created through import (Sources, Purchases)
- Snapshot testing or golden master patterns

## Decisions

### 1. Fixture Structure

Each fixture follows a consistent pattern:

```csharp
public class DevicesFixture
{
    public List<DeviceData> Data { get; } = [];

    public async Task SeedAsync(IAPIRequestContext api, long userId)
    {
        // Fetch sample data from real API
        // Create entities via public REST API
        // Store created entities in Data
    }

    public async Task ClearAsync(IAPIRequestContext api, long userId)
    {
        // Delete created entities via public REST API
    }
}
```

**Rationale**: Consistent API makes fixtures easy to use. The `Data` property allows tests to reference seeded entities by ID, name, or other properties.

### 2. Data Models as Records

Use C# records for data models:

```csharp
public record DeviceData(long Id, string Name, string Icon, string? Color, string? NamingTemplate);
public record PlaylistData(long Id, string Name, int SongsCount);
public record SongData(long Id, string Title, List<ArtistData> Artists, AlbumData Album, List<GenreData> Genres);
```

**Rationale**: Records provide immutability, value equality, and concise syntax. They're ideal for read-only test data.

### 3. Integration with IntegrationTestBase

Tests can use fixtures in two ways:

**Option A: Property injection**
```csharp
public class MyTests : IntegrationTestBase
{
    private readonly DevicesFixture _devices = new();
    private readonly PlaylistsFixture _playlists = new();

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await _devices.SeedAsync(RequestContext, UserId);
        await _playlists.SeedAsync(RequestContext, UserId);
    }

    public override async Task DisposeAsync()
    {
        await _devices.ClearAsync(RequestContext, UserId);
        await _playlists.ClearAsync(RequestContext, UserId);
        await base.DisposeAsync();
    }
}
```

**Option B: Direct instantiation**
```csharp
[Fact]
public async Task Test_WithDevice()
{
    var devices = new DevicesFixture();
    await devices.SeedAsync(RequestContext, UserId);
    // Use devices.Data
}
```

**Rationale**: Option A for tests needing multiple fixtures, Option B for single-fixture tests. Both work with the existing test lifecycle.

### 4. Data Source: Real API

Fetch sample data from `http://localhost/api/*` endpoints:

```csharp
var response = await Playwright.APIRequest.NewContextAsync(new()
{
    BaseURL = "http://localhost",  // Port 80 via Caddy
    ExtraHTTPHeaders = new Dictionary<string, string>
    {
        ["X-MyMusic-UserName"] = "pedro",  // Existing user with data
    },
});

var songsResponse = await response.GetAsync("/api/songs");
var songs = await songsResponse.JsonAsync<List<SongData>>();
```

**Rationale**: Real data provides realistic test scenarios. Using the existing "pedro" user avoids needing to seed production data first.

### 5. Phased Implementation by API Availability

**Phase 1: Entities with existing CREATE APIs**
- `DevicesFixture` → Uses `POST /api/devices`
- `PlaylistsFixture` → Uses `POST /api/playlists`

**Phase 2: Song upload support**
- `SongsFixture` → Uses `POST /api/songs/upload`
- Requires creating minimal test audio file (e.g., empty MP3 or synthetic audio)
- Artists/Albums/Genres created automatically via song metadata

**Phase 3: Backend endpoints needed**
- `ArtistsFixture` → Requires new `POST /api/artists` endpoint
- `AlbumsFixture` → Requires new `POST /api/albums` endpoint
- `GenresFixture` → Requires new `POST /api/genres` endpoint

**Rationale**: Start with what's available, add backend endpoints as needed. This allows incremental progress without blocking.

### 6. File Structure

```
MyMusic.IntegrationTests/
├── Base/
│   └── IntegrationTestBase.cs
├── Fixtures/
│   ├── FixtureBase.cs              # Optional base class with common logic
│   ├── DevicesFixture.cs           # Phase 1
│   ├── PlaylistsFixture.cs         # Phase 1
│   ├── SongsFixture.cs             # Phase 2
│   ├── Models/
│   │   ├── DeviceData.cs
│   │   ├── PlaylistData.cs
│   │   └── SongData.cs
│   └── TestFiles/
│       └── test.mp3                # Minimal test audio for upload
├── Pages/
│   └── ...
└── Tests/
    └── ...
```

### 7. Seeding Strategy

**For entities with direct APIs:**
1. Fetch sample data from real API (or define hardcoded test data)
2. Call CREATE endpoint for each entity
3. Store created entity IDs and data in `Data` property
4. On clear, call DELETE endpoint for each created entity

**For songs (upload API):**
1. Create minimal test audio file
2. Call `POST /api/songs/upload` with multipart form data
3. Parse response to get created song ID
4. Store in `Data` property

**Alternative for songs**: Use the existing test data pattern from `DynamicFilterBuilderSpecs.cs` which creates songs directly in the database. This bypasses the API but is simpler for testing.

### 8. Cleanup Strategy

Fixtures must clean up after tests. Two approaches:

**Approach A: DELETE API calls**
```csharp
public async Task ClearAsync(IAPIRequestContext api, long userId)
{
    foreach (var device in Data)
    {
        await api.DeleteAsync($"/api/devices/{device.Id}");
    }
    Data.Clear();
}
```

**Approach B: Cascade delete via user deletion**
The test user is already deleted in `IntegrationTestBase.DisposeAsync`. If all entities are owned by the user, they cascade delete automatically.

**Decision**: Use Approach B (cascade delete). Since all entities have `OwnerId`, deleting the user will cascade delete songs, playlists, devices, etc. This simplifies cleanup.

## Risks / Trade-offs

- **[Cascade delete dependency]** → If cascade delete is not configured, entities remain after user deletion. Mitigation: verify EF Core cascade delete is configured for all owner-dependent relationships.
- **[File upload complexity]** → Song upload requires multipart form with file. Mitigation: create minimal test MP3 file (empty or synthetic).
- **[Missing APIs]** → Artists, Albums, Genres have no CREATE endpoints. Mitigation: Phase 3 adds these endpoints, or use song upload to create them implicitly.
- **[Test isolation]** → If tests share fixtures, data could interfere. Mitigation: each test creates its own test user with unique fixtures.
