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
