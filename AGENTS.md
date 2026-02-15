# AGENTS.md - MyMusic Project Guidelines

This document provides guidelines for agentic coding agents working on this codebase.

## Project Overview

MyMusic is a .NET 9.0 music management system with:

- **MyMusic.Common** - Shared library with entities, services, EF Core DbContext
- **MyMusic.Server** - ASP.NET Core Web API (REST endpoints)
- **MyMusic.Source** - Additional web project
- **MyMusic.Common.Tests** - xUnit tests with NSubstitute + Shouldly
- **MyMusic.Client** - React/TypeScript SPA with Vite, Mantine UI

## Build Commands

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build MyMusic.Common/MyMusic.Common.csproj
dotnet build MyMusic.Server/MyMusic.Server.csproj

# Run all tests
dotnet test

# Run tests for specific project
dotnet test MyMusic.Common.Tests/MyMusic.Common.Tests.csproj

# Run single test by name
dotnet test --filter "FullyQualifiedName~MusicServiceSpecs.ImportMusic_EmptyDatabase"

# Run tests with verbose output
dotnet test --verbosity detailed

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Database Migrations

```bash
# Create the migration
devbox run db-create-migration "<MigrationName>"

# Restart the server to apply the migration
docker restart my-music-server-1

# Wait for the server to restart
sleep 10s
```

## Generate Client API (Orval)

```bash
# Restart the server to ensure OpenAPI is updated
docker restart my-music-server-1

# Wait for the server to restart
sleep 10s

devbox run orval
```

**Important:** When adding new mutations that should trigger refetch/invalidation of queries, you must add them to the
`mutationInvalidates` array in `orval.config.cjs`. This ensures TanStack Query automatically invalidates the relevant
queries after the mutation completes.

## Code Style Guidelines

### General Conventions

- **.NET 9.0** with `ImplicitUsings` and `Nullable` enabled in all projects
- Use **file-scoped namespaces**: `namespace MyMusic.Common.Services;`
- Use **primary constructors** for controller/service classes
- Use **XML documentation comments** (`/// <summary>`) for public APIs

### Naming Conventions

- **Classes/Types**: PascalCase (`MusicService`, `SongEntity`)
- **Interfaces**: Prefix with `I` (`IMusicService`, `ISource`)
- **Methods**: PascalCase (`ImportRepositorySongs`)
- **Properties**: PascalCase (`OwnerId`, `RepositoryPath`)
- **Private fields**: PascalCase or underscore prefix (follow existing code)
- **Files**: Match class name (`MusicService.cs`)

### Code Patterns

#### Entity Classes (MyMusic.Common/Entities)

```csharp
using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class Song
{
    public long Id { get; set; }

    [MaxLength(256)]
    public required string Title { get; set; }

    public Album Album { get; set; } = null!;
    public long AlbumId { get; set; }
    
    public required List<SongArtist> Artists { get; set; }
}
```

#### Service Classes with Primary Constructor

```csharp
public class MusicService(
    IFileSystem fileSystem,
    IOptions<Config> config,
    ILogger<MusicService> logger) : IMusicService
{
    private readonly AsyncReaderWriterLock _repositoryManagementLock = new();
    // Use primary constructor parameters directly
}
```

#### Controller Classes

```csharp
[ApiController]
[Route("songs")]
public class SongsController(ILogger<SongsController> logger, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet(Name = "ListSongs")]
    public async Task<ListSongsResponse> List(...)
}
```

#### DTOs (Records)

```csharp
public record ListSongsResponse
{
    public required List<ListSongsItem> Songs { get; set; }
}
```

### DTO Patterns

DTOs are organized by resource in `MyMusic.Server/DTO/<Resource>/`. Each response DTO contains nested `Item` classes
with static `FromEntity` methods for mapping from domain entities.

#### Response DTO Structure

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

#### Complex Response DTOs (with related entities)

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

#### Guidelines

- **Use AgileMapper** (`Mapper.Map(entity).ToANew<T>()`) for simple DTOs with direct property mappings
- **Use manual mapping** when you need to transform, order, or include related entities
- **Use inheritance** (e.g., `GetPlaylistSong : ListSongsItem`) to reuse common properties
- **Use aliased imports** (`using Entities = ...`) to avoid ambiguity with domain entities
- **Inherit from `ListSongsItem`** for song-related nested types to reuse its properties

### Imports

```csharp
using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
```

Order: System → Microsoft → Third-party → MyMusic (or use implicit usings)

### Error Handling

- Throw exceptions with descriptive messages: `throw new Exception($"User not found with id {ownerId}")`
- Use try-catch for operations that may fail externally
- Return appropriate HTTP status codes in controllers

### Testing

- Use **xUnit** with `[Fact]` attribute
- Use **Shouldly** for assertions: `songs.Count.ShouldBe(3)`
- Use **NSubstitute** for mocking: `Substitute.For<ILogger<MusicService>>()`
- Use **Scenario** class for test setup (in-memory SQLite + MockFileSystem)
- Follow naming: `<MethodName>_<Scenario>_<ExpectedOutcome>`

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

### Database (EF Core)

- Use **PostgreSQL** with `Npgsql.EntityFrameworkCore.PostgreSQL`
- Use **EFCore.NamingConventions** for snake_case naming
- Use **Include(...).ThenInclude(...)** for related entities
- Use **AsSplitQuery()** for complex queries with includes
- Follow existing migration pattern in `MyMusic.Common/Migrations/`

### Dependencies

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

### Running the Application

```bash
# Development
dotnet run --project MyMusic.Server

# With Docker
docker compose up
```

## MyMusic.Client (React/TypeScript)

```bash
# Install dependencies
cd MyMusic.Client && npm install

# Development server
npm run dev

# Build
npm run build

# Run linter
npm run lint

# Preview build
npm run preview
```

### Known Issues

The auto-generated files in `src/client/` and `src/model/` may contain TypeScript errors. These are pre-existing issues
in the Orval-generated code and should be ignored. Focus on fixing TypeScript errors in manually written code under
`src/routes/`, `src/components/`, and `src/contexts/`.
