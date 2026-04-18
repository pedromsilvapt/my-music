# AGENTS.md - MyMusic Project Guidelines

This document provides guidelines for agentic coding agents working on this codebase.

## Project Overview

MyMusic is a .NET 10.0 music management system with:

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
# Create the migration (requires DesignTimeDbContextFactory in MyMusic.Common)
dotnet ef migrations add <MigrationName> --project MyMusic.Common

# The server applies migrations automatically on startup
```

## Tool calling

Avoid making one tool call at a time. If possible, try to read or write multiple files at once (unless they are really, really big).

## Code Style Guidelines

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

## C# Rules

<!--
    NOTE: When adding/removing rules to this file, always keep them short (1 or 2 lines max for each rule).
          Also, only add generally applicable rules here. Project-specific patterns (with examples and more detail)
          should be added to the project-specific documentation MD file.
-->

- **.NET 10.0** with `ImplicitUsings` and `Nullable` enabled in all projects
- Use **file-scoped namespaces**: `namespace MyMusic.Common.Services;`
- Use **primary constructors** for controller/service classes
- Use **XML documentation comments** (`/// <summary>`) for public APIs
- Imports order: System → Microsoft → Third-party → MyMusic (or use implicit usings)
- Throw exceptions with descriptive messages; return appropriate HTTP status codes in controllers
- Use **PostgreSQL** with `Npgsql.EntityFrameworkCore.PostgreSQL`; `EFCore.NamingConventions` for snake_case
- Use **Include().ThenInclude()** for related entities; **AsSplitQuery()** for complex queries
- **SongDevice Records:** mark for removal (SyncAction = Remove), never delete
- DTOs organized by resource in `MyMusic.Server/DTO/<Resource>/`; AgileMapper for simple, manual mapping for complex
- **One Operation per Service**: naming pattern `<Resource><Operation>Service` (e.g., `SongEditService`, `SongBatchEditService`, `SongDeleteService`); group in folder/namespace `<ResourcePlural>/` when a resource has many services (e.g., `Songs/SongEditService`, `Songs/SongDeleteService`); define interface in Common, implement in Common/Server, register in DI
- Controllers are thin: parse input, delegate to service, map entities↔DTOs, return output; **all business logic lives in services**

## TypeScript Rules

- **ALWAYS** use Orval-generated client APIs; manual `fetch()` is technical debt
- Never manually edit auto-generated files in `src/client/` and `src/model/`
- Customize mutations via hooks in `src/hooks/` wrapping Orval-generated functions
- Ignore TS errors in auto-generated Orval files; fix errors in manually written code only
- When adding mutations with query invalidation, add to `mutationInvalidates` in `orval.config.cjs`

## React & Mantine Rules

- Player Context: `usePlayerActions()` for actions; `usePlayerContext(state => ...)` with selector for reads
- Zustand selectors: always wrap with `useShallow` from `zustand/react/shallow`
- Mantine styles: import all `@mantine` package styles in `src/components/styles.ts` only
- Use `useUncontrolled` from `@mantine/hooks` for controlled/uncontrolled props
- Use `useDebouncedValue` from `@mantine/hooks` for debouncing, not manual `setTimeout`

## React Native & Expo Rules

- Use `configService` for all config access; never modify AsyncStorage or use stores for persisted config
- Zustand for UI state only; `configService` for persisted config
- API client: manual fetch + Zod for validation
- Auth via headers (`X-MyMusic-UserId`, `X-MyMusic-UserName`) stored in SecureStore

## Project Documentation

Before working on any project, read its development guide first:

- **Common** and **Server** → [docs/development/server.md](docs/development/server.md)
- **Client** → [docs/development/client.md](docs/development/client.md)
- **CLI** → [docs/development/cli.md](docs/development/cli.md)
- **Mobile** → [docs/development/mobile.md](docs/development/mobile.md)

These files contain information related to the creating and running automatic tests for each sub-project as well.

## Active Technologies

- .NET 10.0 (backend), TypeScript/React (frontend) + Entity Framework Core, PostgreSQL, TanStack Query, Zustand, Refit (003-metadata-auto-fetch)
- PostgreSQL with EF Core, JsonElement for metadata patch storage (003-metadata-auto-fetch)

## Technical Debt Management

The project maintains a **TECHDEBT.md** file that tracks all identified code quality issues across the codebase. When working on the codebase, you should reference this file to understand and address technical debt systematically.

### Reference

- **TECHDEBT.md Location:** `/workspaces/my-music/TECHDEBT.md`
- **Total Tasks:** 60 technical debt items across 7 categories
- **Categories:** Backend Duplication, Backend SRP Violations, Backend Consistency, DTO Consistency, Frontend Violations, Cross-Project Utilities, Testing Patterns

### Mandatory Testing Rule (CRITICAL)

**All technical debt tasks MUST include test creation BEFORE implementing changes**, with these exceptions:

- Typo fixes in comments or strings
- Comment additions or updates
- Whitespace/formatting changes
- File renames without logic changes
- Simple code moves without behavior changes

**Process for technical debt tasks:**
1. Write tests verifying the CURRENT behavior
2. Run tests to ensure they pass
3. Implement the refactoring
4. Run tests again to verify nothing broke
5. Update the checkbox `[ ]` to `[x]` in TECHDEBT.md

**If you cannot write tests beforehand, you MUST ask the user for permission before proceeding.** Technical debt solutions should NEVER change functionality.

### Working with TECHDEBT.md

**Task Selection:**
- High Severity + High Impact: Do first (critical code quality issues)
- High Severity + Low Impact: Do soon (important but less urgent)
- Low Severity + High Impact: Do when convenient (good ROI)
- Low Severity + Low Impact: Do last or skip (nice to have)

**Completion Checklist:**
- [ ] Task selected from TECHDEBT.md
- [ ] Tests written (if applicable)
- [ ] Tests pass before changes
- [ ] Refactoring implemented
- [ ] Tests pass after changes
- [ ] Checkbox updated in TECHDEBT.md
- [ ] Commit message references task ID (e.g., "TD0042 - Fix useShallow violation")

### Why This Matters

Technical debt accumulation leads to:
- Increased bug rates
- Slower feature development
- Higher maintenance costs
- Developer onboarding friction

By systematically addressing tech debt with proper testing, we maintain code quality while improving the codebase over time.
