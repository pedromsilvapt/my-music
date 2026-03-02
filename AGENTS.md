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

DTOs are organized by resource in `MyMusic.Server/DTO/<Resource>/`.

#### File Organization Rules

1. **Request DTOs** → Separate file per request
    - `CreatePlaylistRequest.cs`
    - `UpdatePlaylistRequest.cs`

2. **Response DTOs** → Separate file per response
    - `CreatePlaylistResponse.cs` - may contain nested `*Item` classes used only in this response
    - `GetPlaylistResponse.cs` - may contain nested `*Item` classes used only in this response

3. **Shared Data DTOs** → Defined in `Shared.cs` if used across multiple requests/responses
    - `SyncFileInfoItem` used in both `SyncCheckRequest` and `SyncCheckResponse`

#### Example: Devices Resource

```
DTO/Devices/
  CreateDeviceRequest.cs       # Request only
  CreateDeviceResponse.cs       # Response + CreateDeviceItem (nested)
  ListDevicesResponse.cs        # Response + ListDeviceItem (nested)
```

#### Example: Sync Resource

```
DTO/Sync/
  Shared.cs                    # SyncFileInfoItem (shared data)
  SyncCheckRequest.cs          # Request (references Shared)
  SyncCheckResponse.cs         # Response (references Shared)
  SyncUploadResponse.cs        # Response only
```

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

### Important: Never Manually Edit Auto-Generated Files

The files in `src/client/` and `src/model/` are **auto-generated by Orval** from the OpenAPI specification. **Never
manually edit these files** - your changes will be overwritten the next time Orval runs.

If you need to customize mutation behavior (e.g., add query invalidations), create a custom hook in `src/hooks/` that
wraps the auto-generated mutation functions. For example:

```typescript
// src/hooks/use-custom-mutation.ts
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useSomeGeneratedMutation} from "../client/some-api.ts";

export function useCustomMutation() {
    const queryClient = useQueryClient();
    
    return useMutation(
        {
            ...useSomeGeneratedMutation({}),
            onSuccess: () => {
                // Custom invalidation logic here
                queryClient.invalidateQueries({queryKey: ['some', 'queries']});
            }
        },
        queryClient
    );
}
```

To regenerate the client API after backend changes, run:

```bash
devbox run orval
```

### Player Context Usage Guidelines

When working with the player context (`src/contexts/player-context.tsx`), always follow these rules:

1. **For performing actions** (play, pause, skip, add to queue, etc.):
    - Always use `usePlayerActions()` hook
    - Example: `const playerActions = usePlayerActions(); playerActions.play(songs);`

2. **For reading properties** (current song, queue, volume, etc.):
    - Always use `usePlayerContext(state => ...)` with a selector function
    - Example: `const currentSong = usePlayerContext(state => state.current);`
    - Never call `usePlayerContext()` without a selector - it returns the entire store API and causes excessive
      re-renders

This ensures proper reactivity and prevents unnecessary component re-renders.

### Zustand Store Usage Guidelines

When using Zustand stores with selectors, **always wrap your selector function with `useShallow`** to prevent
unnecessary re-renders:

```typescript
import {useShallow} from 'zustand/react/shallow';

// Good - useShallow prevents re-renders when selector returns a new object
const {time, duration} = usePlaybackStore(
    useShallow((state) => {
        if (state.current.type === 'LOADED') {
            return {time: state.current.time, duration: state.current.duration};
        }
        return {time: 0, duration: 0};
    })
);

// Bad - returning complex objects without useShallow causes infinite re-renders
const {time, duration} = usePlaybackStore((state) => {
    return {time: state.current.time, duration: state.current.duration}; // new object every render!
});
```

**Why?** Zustand compares selectors by reference. When your selector returns a new object/array on every render, React
thinks the state changed every render, causing infinite update loops. `useShallow` performs a shallow equality check
instead.

### Known Issues

The auto-generated files in `src/client/` and `src/model/` may contain TypeScript errors. These are pre-existing issues
in the Orval-generated code and should be ignored. Focus on fixing TypeScript errors in manually written code under
`src/routes/`, `src/components/`, `src/contexts/`, and `src/hooks/`.

### Mantine Styles

All Mantine package styles should be imported in `src/components/styles.ts`. When adding a new @mantine package that
requires styles (e.g., `@mantine/dates`), add the import there instead of in individual components.

### Debouncing Pattern

When implementing search, filter, or other input-based functionality that requires debouncing, **always
use `useDebouncedValue` from `@mantine/hooks`** instead of manual `useEffect` + `setTimeout` patterns:

```typescript
import {useDebouncedValue} from "@mantine/hooks";

const SEARCH_DEBOUNCE_MS = 300;

// Good - useDebouncedValue handles cleanup automatically
const [debouncedSearch] = useDebouncedValue(searchQuery, SEARCH_DEBOUNCE_MS);

useEffect(() => {
    performSearch(debouncedSearch);
}, [debouncedSearch]);

// Bad - manual setTimeout requires manual cleanup
useEffect(() => {
    const timer = setTimeout(() => {
        performSearch(searchQuery);
    }, 300);
    return () => clearTimeout(timer);
}, [searchQuery]);
```

The Mantine hook is cleaner, avoids potential memory leaks from forgotten cleanup, and is consistent with existing
codebase patterns.

## MyMusic.Mobile (React Native)

MyMusic.Mobile is a React Native (Expo) mobile application that provides the same sync functionality as MyMusic.CLI but
with a mobile-friendly interface.

### Technology Stack

- **Framework**: Expo SDK 55 with Expo Router for navigation
- **Language**: TypeScript
- **State Management**: Zustand for UI state only
- **Config Management**: Centralized configService (single source of truth)
- **UI**: React Native built-in components with custom styling
- **API Client**: Manual fetch with Zod for validation
- **Storage**: AsyncStorage for config, SecureStore for credentials

### Project Structure

```
MyMusic.Mobile/
├── app/                          # Expo Router routes (file-based routing)
│   ├── _layout.tsx               # Root layout
│   ├── index.tsx                 # Home/Dashboard screen
│   ├── settings/
│   │   ├── index.tsx             # Settings main
│   │   └── device.tsx            # Device configuration
│   ├── history/
│   │   ├── index.tsx             # Sessions list
│   │   └── [sessionId].tsx       # Session detail
│   └── sync/
│       └── progress.tsx          # Active sync screen
├── src/
│   ├── api/                      # API client & types
│   │   ├── client.ts             # Base fetch wrapper with auth
│   │   ├── devices.ts            # Device API functions
│   │   ├── sync.ts               # Sync API functions
│   │   └── types.ts              # Zod schemas
│   ├── stores/                   # Zustand stores (UI state only)
│   │   ├── configStore.ts        # UI loading state
│   │   ├── authStore.ts          # Auth state
│   │   └── syncStore.ts          # Sync progress state
│   ├── services/                 # Business logic
│   │   ├── configService.ts      # Centralized config management (single source of truth)
│   │   ├── fileScanner.ts        # Music file scanner
│   │   └── syncService.ts        # Core sync orchestration
│   ├── components/ui/            # Reusable UI components
│   └── constants/                # Theme & device icons
└── app.json
```

### Important: Use configService for All Config Access

All configuration (server URL, device settings, user info) should be accessed through `configService`. Never directly
modify AsyncStorage or use separate stores for persisted config.

```typescript
// Good - use configService for all config access
import { getServerUrl, setServerUrl, getUserName, getDeviceId, ... } from './services/configService';

// Bad - don't use separate stores for persisted config
import { useConfigStore } from './stores/configStore';  // Only for UI state
```

The configService provides:

- **Single source of truth** - All config goes through one service
- **Automatic sync** - Setting a value updates both runtime AND storage
- **Type-safe** - All config access goes through proper getters/setters

### Running the App

```bash
# Install dependencies
cd MyMusic.Mobile && npm install

# Start Metro bundler
npm start

# Run on iOS simulator
npm run ios

# Run on Android emulator
npm run android

# Build for production
npx expo prebuild
npx expo run:android
```

### Key Features

1. **Configuration**: Set server URL, username, device name, device type, and repository path
2. **Sync Music**: Upload local music to server, download server music to device
3. **View History**: See past sync sessions with detailed records
4. **Progress Tracking**: Real-time sync progress with counts and ETA

### API Integration

The mobile app uses the same API endpoints as the web client and CLI:

- Device management: `/api/devices`
- Sync operations: `/api/devices/{deviceId}/sync/*`
- Sessions: `/api/devices/{deviceId}/sessions`

Authentication is handled via headers (`X-MyMusic-UserId`, `X-MyMusic-UserName`) stored securely.
