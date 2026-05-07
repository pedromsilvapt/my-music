# MyMusic.Server Development Guide

## DTO Patterns

DTOs are organized by resource in `MyMusic.Server/DTO/<Resource>/`.

### File Organization Rules

1. **Request DTOs** → Separate file per request
    - `CreatePlaylistRequest.cs`
    - `UpdatePlaylistRequest.cs`

2. **Response DTOs** → Separate file per response
    - `CreatePlaylistResponse.cs` - may contain nested `*Item` classes used only in this response
    - `GetPlaylistResponse.cs` - may contain nested `*Item` classes used only in this response

3. **Shared Data DTOs** → Defined in `Shared.cs` if used across multiple requests/responses
    - `SyncFileInfoItem` used in both `SyncCheckRequest` and `SyncCheckResponse`

### Example: Devices Resource

```
DTO/Devices/
  CreateDeviceRequest.cs       # Request only
  CreateDeviceResponse.cs       # Response + CreateDeviceItem (nested)
  ListDevicesResponse.cs        # Response + ListDeviceItem (nested)
```

### Example: Sync Resource

```
DTO/Sync/
  Shared.cs                    # SyncFileInfoItem (shared data)
  SyncCheckRequest.cs          # Request (references Shared)
  SyncCheckResponse.cs         # Response (references Shared)
  SyncUploadResponse.cs        # Response only
```

### Response DTO Structure

```csharp
using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Playlists;

public record CreatePlaylistResponse
{
    public required CreatePlaylistItem Playlist { get; init; }
}

public record CreatePlaylistItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }

    public static CreatePlaylistItem FromEntity(Entities.Playlist playlist) =>
        Mapper.Map(playlist).ToANew<CreatePlaylistItem>();
}
```

### Complex Response DTOs (with related entities)

When mapping entities with relationships, use manual mapping to control the output:

```csharp
using MyMusic.Server.DTO.Songs;
using Entities = MyMusic.Common.Entities;
using SongEntity = MyMusic.Common.Entities.Song;

namespace MyMusic.Server.DTO.Playlists;

public record GetPlaylistResponse
{
    public required GetPlaylistItem Playlist { get; init; }
}

public record GetPlaylistItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required List<GetPlaylistSong> Songs { get; init; }

    public static GetPlaylistItem FromEntity(Entities.Playlist playlist) =>
        new GetPlaylistItem
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Songs = playlist.PlaylistSongs
                .OrderBy(ps => ps.Order)
                .Select(ps => GetPlaylistSong.FromEntity(ps.Song, ps.Order, ps.AddedAt))
                .ToList()
        };
}

public record GetPlaylistSong : ListSongsItem
{
    public required int Order { get; init; }
    public DateTime? AddedAtPlaylist { get; init; }

    public static GetPlaylistSong FromEntity(SongEntity song, int order, DateTime addedAt) =>
        new GetPlaylistSong
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Artists = song.Artists.Select(a => ListSongsArtist.FromEntity(a.Artist)).ToList(),
            Album = ListSongsAlbum.FromEntity(song.Album),
            Genres = song.Genres.Select(g => ListSongsGenre.FromEntity(g.Genre)).ToList(),
            Year = song.Year,
            Duration = $"{Convert.ToInt32(song.Duration.TotalMinutes)}:{song.Duration.Seconds:00}",
            IsFavorite = false,
            IsExplicit = song.Explicit,
            CreatedAt = song.CreatedAt,
            AddedAt = song.AddedAt,
            Order = order,
            AddedAtPlaylist = addedAt
        };
}
```

### Guidelines

- **Use AgileMapper** (`Mapper.Map(entity).ToANew<T>()`) for simple DTOs with direct property mappings
- **Use manual mapping** when you need to transform, order, or include related entities
- **Use inheritance** (e.g., `GetPlaylistSong : ListSongsItem`) to reuse common properties
- **Use aliased imports** (`using Entities = ...`) to avoid ambiguity with domain entities
- **Inherit from `ListSongsItem`** for song-related nested types to reuse its properties

## Cross-Project Configuration Access Pattern

Use this pattern when a service in `MyMusic.Common` needs access to configuration or values only available in `MyMusic.Server`:

1. **Define interface in Common** - Declare the required values
2. **Implement in Server** - Access actual configuration source
3. **Register in DI** - Simple type registration

### Existing Examples (see code for implementation details)

- **ICurrentUser / HttpCurrentUser** - Access to current user from HTTP context
- **IApiPathResolver / ApiPathResolver** - Access to server configuration values

## Imports

```csharp
using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
```

Order: System → Microsoft → Third-party → MyMusic (or use implicit usings)

## Error Handling

- Throw exceptions with descriptive messages: `throw new Exception($"User not found with id {ownerId}")`
- Use try-catch for operations that may fail externally
- Return appropriate HTTP status codes in controllers

## Testing

- Use **xUnit** with `[Fact]` attribute
- Use **Shouldly** for assertions: `songs.Count.ShouldBe(3)`
- Use **NSubstitute** for mocking: `Substitute.For<ILogger<MusicService>>()`
- Use **Scenario** class for test setup (in-memory SQLite + MockFileSystem)
- Follow naming: `<MethodName>_<Scenario>_<ExpectedOutcome>`
- **Arrange-Act-Assert (AAA) pattern**: Every test method must clearly delineate its sections with `// Arrange`, `// Act`, and `// Assert` comments. When Act and Assert are trivially combined (e.g., a single expression that both calls and asserts), they may be merged as `// Act & Assert`. When there is no Arrange step and the test jumps straight to assertions on static data, use `// Arrange` followed by `// Assert` (skipping Act).

### Assertion Style Guidelines

Prefer **direct assertions** over `ShouldSatisfyAllConditions` for clarity and better error messages:

```csharp
// Good - Direct assertions with clear failure messages
songs.Count.ShouldBe(3);
songs[0].Title.ShouldBe("Song Title");
songs[0].Artists.Count.ShouldBe(2);

// Avoid - Multiple conditions in one assertion
songs.ShouldSatisfyAllConditions(
    () => songs.Count.ShouldBe(3),
    () => songs[0].Title.ShouldBe("Song Title"),
    () => songs[0].Artists.Count.ShouldBe(2)
);
```

Use `ShouldSatisfyAllConditions` only when you need to assert multiple independent conditions on the same object and want all failures reported at once:

```csharp
// Acceptable - When multiple unrelated properties need verification
user.ShouldSatisfyAllConditions(
    () => user.Name.ShouldNotBeNull(),
    () => user.Email.ShouldContain("@"),
    () => user.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue)
);
```

```csharp
[Fact]
public async Task ImportMusic_EmptyDatabase()
{
    var scenario = new Scenario();
    var musicService = scenario.CreateMusicService();

    // Act
    await musicService.ImportRepositorySongs(...);

    // Assert
    job.SkipReasons.ShouldBeEmpty();
    songs.Count.ShouldBe(3);
}
```

## Integration Testing

Integration tests in **MyMusic.IntegrationTests** verify end-user functionality through Playwright browser interactions. They focus on user-visible behavior, not implementation details.

### Test Organization

| Test Type | Location | Purpose |
|-----------|----------|---------|
| Unit Tests | MyMusic.Common.Tests | Business logic, services, algorithms |
| Integration Tests | MyMusic.IntegrationTests | Playwright browser tests, end-user functionality |

### Writing Integration Tests

All integration tests should inherit from `IntegrationTestBase`, which provides:

- **Automatic user lifecycle**: Creates a test user during initialization, deletes it during disposal
- **APIRequestContext**: Pre-configured with auth headers (`X-MyMusic-UserName`)
- **Protected properties**: `UserId` and `UserName` for the test user

```csharp
using Microsoft.Playwright.Xunit;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests;

public class MyTest : IntegrationTestBase
{
    [Fact]
    public async Task ShouldDisplaySeededSongs()
    {
        // Arrange
        var songs = new SongsFixture();
        await songs.SeedAsync(RequestContext, UserId);

        // Act
        var home = new HomePage(Page);

        // Navigate using page objects (already waits for initial data load)
        var songsPage = await home.Navbar.GoToSongsAsync();

        // Assert
        var songTitles = await songsPage.Collection.GetTitleTextsAsync();
        songTitles.ShouldContain("Test Song");
    }
}
```

**Key learnings:**

- **Page Object Model**: Pages receive `IPage` in their constructor; components receive a scoped `ILocator`. This encapsulates selectors and interactions, making tests more maintainable.
- **Fixtures for test data**: Use fixtures to seed test data via the REST API. Each fixture has a `Data` property to access the seeded entities. Fixtures can be reused across tests.
- **Relative URLs in tests**: Always use relative URLs (e.g., `/api/songs`) instead of absolute URLs. The `IAPIRequestContext` is already configured with the base URL.
- **Sample data as immutable records**: Use immutable C# records (e.g., `SampleSong`) for test data, not anonymous types. This provides type safety, reusability, and better IDE support.

### Running Integration Tests

```bash
# Run all integration tests
dotnet test MyMusic.IntegrationTests

# Run specific test
dotnet test --filter "FullyQualifiedName~TestUserDisplayTests"

# Run with verbose output
dotnet test MyMusic.IntegrationTests --verbosity detailed
```

### Integration Test Guidelines

- **Test user-visible behavior**: Clicks, navigation, displayed content
- **Don't test internals**: Service methods, database state directly
- **Use stable selectors**: Prefer `data-testid` attributes over fragile CSS selectors
- **Keep tests focused**: One user workflow per test
- **Clean up automatically**: `IntegrationTestBase` handles user deletion
- **Page Object Navigators**: Components and Pages should include `GoTo*` or `Open*` that navigate to other pages/models
    - **Return the target object model** The new `Page` or `Component` (mostly for modals)
    - **Wait for initial data load** in the naviation method itself, so callers receive an object when data is available in the browser (look for `CollectionComponent` for example)

### Test Fixtures

Use fixtures to seed test data via the REST API. Each fixture has a `Data` property with seeded entity data:

```csharp
public class MyTests : IntegrationTestBase
{
    [Fact]
    public async Task Test_WithDevices()
    {
        var devices = new DevicesFixture();
        await devices.SeedAsync(RequestContext, UserId);

        // Use devices.Data to access seeded devices
        devices.Data[0].Id.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Test_WithAllData()
    {
        var allData = new AllDataFixture();
        await allData.SeedAsync(RequestContext, UserId);

        // All fixtures are seeded: Devices, Playlists, Songs, Artists, Albums, Genres
    }
}
```

**Available Fixtures:**
- `DevicesFixture` - Creates test devices via `POST /api/devices`
- `PlaylistsFixture` - Creates test playlists via `POST /api/playlists`
- `SongsFixture` - Uploads test songs via `POST /api/songs/upload`
- `ArtistsFixture` - Creates test artists via `POST /api/artists`
- `AlbumsFixture` - Creates test albums via `POST /api/albums` (requires ArtistsFixture)
- `GenresFixture` - Creates test genres via `POST /api/genres`
- `AllDataFixture` - Composite fixture that seeds all the above

## Database (EF Core)

- Use **PostgreSQL** with `Npgsql.EntityFrameworkCore.PostgreSQL`
- Use **EFCore.NamingConventions** for snake_case naming
- Use **Include(...).ThenInclude(...)** for related entities
- Use **AsSplitQuery()** for complex queries with includes
- Follow existing migration pattern in `MyMusic.Common/Migrations/`
- **SongDevice Records:** When deleting songs that have been synced to devices, always mark SongDevice records for removal (set SongId = null, SyncAction = Remove) instead of deleting them. This allows the sync system to track and remove files from devices during the next sync operation. See AuditsController.ResolveSoundalikes for example.

## Dependencies

Key packages used:

- `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL`
- `xunit` + `xunit.runner.visualstudio`
- `NSubstitute` for mocking
- `Shouldly` for assertions
- `Refit` for HTTP client generation
- `taglib-sharp-netstandard2.0` for audio metadata
- `System.IO.Abstractions` + `TestableIO.System.IO.Abstractions` for testable file I/O

## Common Tasks

### Adding a New Entity

1. Create entity class in `MyMusic.Common/Entities/`
2. Add to `MusicDbContext`
3. Add migration: `dotnet ef migrations add AddNewEntity`
4. Create DTOs in `MyMusic.Server/DTO/`
5. Add controller endpoints

### Adding a New API Endpoint

1. Create DTOs in appropriate `MyMusic.Server/DTO/` folder
2. Add method to service interface/implementation
3. Add controller action with proper HTTP attribute
4. Add test if applicable

## Running the Application

```bash
# Development
dotnet run --project MyMusic.Server

# With Docker
docker compose up
```

## OpenTelemetry

The server supports OpenTelemetry tracing and logging, disabled by default.

### Configuration

Add to `appsettings.json` or set environment variables:

```json
{
  "OpenTelemetry": {
    "Enabled": false,
    "Endpoint": "http://localhost:4317",
    "Protocol": "grpc"
  }
}
```

| Environment Variable | Description | Default |
| --- | --- | --- |
| `OpenTelemetry__Enabled` | Enable OpenTelemetry | `false` |
| `OpenTelemetry__Endpoint` | OTLP base endpoint | `http://localhost:4317` |
| `OpenTelemetry__Protocol` | Export protocol (`grpc` or `http/protobuf`) | `grpc` |
| `OpenTelemetry__TracesEndpoint` | Override traces endpoint (optional) | _(Endpoint + /v1/traces)_ |
| `OpenTelemetry__LogsEndpoint` | Override logs endpoint (optional) | _(Endpoint + /v1/logs)_ |
| `OpenTelemetry__MetricsEndpoint` | Override metrics endpoint (optional) | _(Endpoint + /v1/metrics)_ |

### Setup

1. Start a collector: Otelite (`docker compose up otelite` or `./tools/otelite server`)
2. Set `OpenTelemetry__Enabled=true` (or set `OpenTelemetry:Enabled` to `true` in appsettings)
3. For Otelite, also set `OpenTelemetry__Endpoint=http://localhost:4318` and `OpenTelemetry__Protocol=http/protobuf`
4. Start the server
5. Traces and logs will be sent to the configured collector

### Graceful Degradation

If the OTLP endpoint is unavailable, the server continues operating normally. No errors are thrown and no functionality is impacted.
