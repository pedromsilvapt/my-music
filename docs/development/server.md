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

### Concurrent Song Imports

`MusicService.ImportRepositorySongs` is called concurrently by uploads, sync commits, purchases and shared-song imports, and each song is imported in its own transaction. Find-or-create logic is kept safe like this:

- **Genres**: lock-free. `UserMusicService.UpsertGenre` uses `INSERT ... ON CONFLICT DO NOTHING` on the unique `(owner_id, name)` index. Upsert them in a stable (sorted) order.
- **Artists and albums**: their names cannot be unique, so no constraint can protect them. The import takes **advisory locks** (`IAdvisoryLockService`, `pg_advisory_xact_lock`) on the song's artist names, album, checksum, repository path and (when updating) song id, before touching the database. Songs that share none of these keys import in parallel.
- **Per-user cap**: `IUserImportThrottle` limits concurrent song imports per user (`MyMusic:MaxConcurrentImportsPerUser`, default 16). This bounds DB connections and I/O; correctness does not depend on it.
- **Retries**: a song failing with a deadlock, serialization failure or unique violation (e.g. against writers that don't take the locks) is rolled back and retried up to 3 times. The rolled-back attempt's tracked entities are discarded, and its file changes undone (see [Transactional file operations](#transactional-file-operations)).

New code that finds-or-creates artists or albums should take the same `AdvisoryLockKey`s (build them with `AlbumArtistLockKeys.Create`). Song edits (`SongUpdateService`) do: an API edit references its album by name and album artist, never by id, and finds-or-creates it among that artist's albums through `IAlbumUpsertService`. The album artist must be one of the song's artists, and the album and artists an edit leaves unused are deleted. Unit tests run on SQLite, which has no advisory locks, so they use `InProcessAdvisoryLockService` (in `MyMusic.Common.Tests/Utilities`) instead.

### Updating many songs in one transaction

Operations that change what many songs say about themselves (renaming, merging or deleting an album or artist) must not rewrite tags, checksums, device marks, labels or paths themselves. They change the album/artist rows and push every affected song (`AlbumArtistSongsQuery.OfAlbum` / `OfArtist`) through `ISongUpdateService.UpdateSongsAsync`, which runs the same per-song update as a song edit, inside the caller's transaction:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
await using var files = fileTransactions.Begin(db);

// The handle must be disposed only after the transaction has ended (see AdvisoryLockHolder)
var locks = await advisoryLocks.AcquireTransactionLocksAsync(db,
    AlbumArtistLockKeys.Create(ownerId, artistNames, albums), cancellationToken);
// ... change the album/artist rows ...
await songUpdate.UpdateSongsAsync(db, files, updates, options, cancellationToken);
await transaction.CommitAsync(cancellationToken);
```

- **All or nothing**: `UpdateSongsAsync` never commits and throws on the first failing song. Letting the transaction roll back restores every song's rows and files.
- **Locks once, up front**: `UpdateSongsAsync` takes no artist/album locks. The caller acquires, in a single `AcquireTransactionLocksAsync` call (which orders them canonically), the keys of every artist name and (album artist name, album name) involved: old names, new names and placeholders. Acquiring them song by song could deadlock against imports.
- **An empty `SongUpdateModel`** re-applies the song's current rows to its file: use it after renaming an album/artist row in place.
- **Album by id**: `new AlbumRef { Id = ... }` moves a song to that exact album (owner-checked) instead of finding-or-creating one by name. It is server-side only, and is not read from API requests.
- **`SongUpdateOptions.KeepAlbumIds` / `KeepArtistIds`** exclude rows from the unused album/artist cleanup, for rows the operation still needs while songs are moving.

### Transactional file operations

Song imports and song edits change files in the music repository inside a database transaction. `IFileTransactionService.Begin(db)` binds an `IFileTransaction` to the context's current transaction, so the file changes are undone when that transaction is rolled back, fails to commit, or is disposed without committing (via `FileTransactionInterceptor`, which must be registered on the context). A commit keeps the changes. Declare it with `await using` right after the database transaction.

| Operation | Use it to | Undone by |
|---|---|---|
| `PrepareOverwriteAsync(path)` | write a file from scratch; an existing file is moved aside | deleting the new file, moving the original back |
| `PrepareEditAsync(path)` | edit a file in place (e.g. TagLib); a copy is kept | moving the copy back over it |
| `MoveAsync(from, to)` | move a file; never overwrites | moving it back |
| `DeleteAsync(path)` | delete a file, by moving it aside | moving it back |

- Backups live in `<MusicRepositoryPath>/.temp/tx-{guid}/` (same volume, so setting a file aside is a rename), with a `backups.txt` listing their original paths. `.temp` gets a `.musicignore`.
- Each operation takes an advisory lock on the paths it touches (`AdvisoryLockScope.File`), held until the transaction ends.
- If an undo step fails, or the process dies mid-transaction, the `tx-*` folder is kept for manual recovery; nothing deletes it automatically.

### Song history

PostgreSQL triggers queue a snapshot of a song (built by the `song_history_build_snapshot` SQL function) before every change to it, and `SongHistoryWorker` turns each transaction's queued snapshots into one `song_histories` revision holding the fields that changed. A field only shows up in the history if that function reads it.

#### Merged songs

When a song absorbs another one, a `song_merges` row (`SongMerge`) records it: the kept song's id, the merged song's id, and the `Kind` (`ImportDuplicate`, `SoundalikeMerge` or `SoundalikeDelete`). The snapshot lists the songs merged directly into a song as `merged_songs`, so the merge is part of the kept song's history.

Resolving a soundalike group (`SoundalikeResolutionService`) gives each song other than the kept one a `SecondaryAction`:

- **`Delete` / `Merge`**: the song is absorbed by the kept song and deleted (`Merge` copies its missing metadata first). The kept song takes the oldest `CreatedAt` and `AddedAt` among itself and the absorbed songs, and its `ModifiedAt` becomes now. After a `Merge`, the merged metadata is written to the kept song's file (`ISongFileUpdateService`, shared with song edits): only if the checksum changes is `FileModifiedAt` updated and the song's devices marked for download.
- **`Ignore`**: the song is left untouched, and an `ExcludedDuplicatePair` with the kept song stops the pair from being reported again.

- **No song FKs**: the merged song is deleted by the merge, and the kept song may itself be merged or deleted later. Only `owner_id` has an FK, which cascades when the user is deleted.
- **Tree, not flattened**: rows are never updated, copied or deleted. Merging A into B, then B into C, leaves two rows (A→B, B→C); `merged_songs` of C lists only B. Walk `song_merges` recursively for the full lineage. `merged_song_id` is unique, since a song is merged away only once.
- **Flush first in `SongMergeService`**: the worker uses the first snapshot queued in a transaction as the revision's "before" state. The merge re-points the merged song's artists, genres and devices with `UPDATE song_id`, which queues nothing, so the `SongMerge` row is saved before those steps: its insert trigger snapshots the kept song while it is still untouched, and the gained artists/genres land in the merge's revision.

## Dependencies

NuGet versions are managed centrally in the root `Directory.Packages.props`. To add or bump a package, set its `<PackageVersion>` there and reference it from the `.csproj` without a `Version` attribute.

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
