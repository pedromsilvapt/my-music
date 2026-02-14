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
# Add migration (run from MyMusic.Common directory)
dotnet ef migrations add <MigrationName> --project MyMusic.Common

# Apply migrations
dotnet ef database update --project MyMusic.Common
```

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
    public required List<ListSongsItem> Songs { get; init; }
}
```

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
