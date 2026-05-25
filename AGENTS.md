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

# Run test project as standalone executable (xUnit v3 feature)
dotnet run --project MyMusic.Common.Tests
```

**Note:** Test projects use xUnit v3 and are standalone executables (`<OutputType>Exe</OutputType>`). They can be run via `dotnet test` (standard) or `dotnet run` (executable mode).

## Git Commands
**CRITICAL** Do not, under any circunstance, execute git write commands (commit, stash, reset, checkout, etc..) without the user's explicit instruction.

## Integration Tests

- **MyMusic.IntegrationTests** - Playwright browser tests for end-user functionality; inherit from `IntegrationTestBase` for automatic user lifecycle
- Run with: `dotnet test MyMusic.IntegrationTests` or filter by name: `dotnet test --filter "FullyQualifiedName~TestName"`
- The default configured RunSettings file is `MyMusic.IntegrationTests/.runsettings`
- **IMPORTANT** You can run all unit tests, and you can run specific integration tests, but never run all tests (or all integration tests).

### Containerized Integration Tests (CI Debugging)

Use containerized tests **only** for debugging CI-specific failures. For local development, use `dotnet test` instead.

```bash
# Build the test image
earth +docker-integration-tests

# Run tests with all dependencies (postgres, server, client, caddy)
docker compose -f compose.integration.yaml up --exit-code-from integration-tests

# Cleanup containers and volumes
docker compose -f compose.integration.yaml down --volumes
```

The compose file spins up a complete test environment (PostgreSQL, Server API, Client SPA, Caddy reverse proxy).

- All test classes should inherit from the base `IntegrationTestBase` and should be placed in `Tests/<Domain>/<Name>Tests.cs`
- Fixtures should live in the folder `Fixtures/`. Do not set up data on the tests themselves!
- When instantiating fixture classes, prefer class variables over recreating the same fixture object in multiple tests on the same file.
- Test classes should never have Playwright locators declared directly inside them. All locators should live in `Pages/<Name>Page.cs` and `Pages/Components/<Name>Component.cs` classes
    - Page objects can be composed of other Component objects via Properties, but can never inherit them
    - Page objects should receive an `IPage` in their constructor, example: `public class HomePage(IPage page) : BasePage(page)`
    - Component objects should receive a scoped `ILocator` in their constructor, example: `public class ButtonComponent(ILocator locator) : BaseComponent(locator)`
    - Prefer primary class constructors
- Test cases should have very simple logic. Use helper methods, fixtures, component objects to hide implementation details. Make liberal use of flows to keep the logic in the test simple and expressive.

### Concepts
 - **Page Object**: Represents a route page in the Client application
 - **Component Object**: Represents a sub-component of a page, or of another component. Should be reusable. Can be a button, modal dialog, etc...
 - **Fixture**: An object intended to setup the environment with known data to allow the tests to run.
 - **Flow**: A reusable, configurable, multi-step **action** or **validation** (optionally, containing flows as well) that can be reused across multiple tests to express user **intent** on what the test does. Optionally, can also return a value (a page object, a result, etc...)

- When locating elements with Playwright, prefer:
    - Simple `GetByRole` / `GetByLabel` when the elements are uniquely identifiable by such
    - Otherwise, consider adding `GetByTestId(...)` / `data-testid="..."` over building complex & flaky locator queries in the tests

### Data Loading Convention for Page Components

When a page fetches data, add `data-testid="<page-name>"` and `data-loading` to the root element to enable reliable test waiting:

- **On loading fallbacks**: `data-loading="true"` — e.g., `<Box data-testid="song-detail" data-loading="true">Loading...</Box>`
- **On main content**: `data-loading={query.isFetching ? "true" : "false"}` — e.g., `<Box data-testid="song-detail" data-loading={songQuery.isFetching ? "true" : "false"}>`
- **In page objects**: `WaitForLoadedAsync` should wait for the element, then assert `data-loading="false"`
- **`<Collection>` already follows this convention** via its built-in `data-loading` attribute — no extra work needed for list pages
- Use the existing component's root element, no need to create a `<Box>` just for this.

### Comment Style

Integration tests should use comments with these characteristics: (example based on the test `Sync_ShouldUploadLocalChangesToServer` in [CliSyncTests.cs](./MyMusic.IntegrationTests/Tests/Cli/CliSyncTests.cs)):

1. **Intent-focused**: Comments explain *what* and *why*, not just restating code
2. **Block-level grouping**: Each logical section gets a comment marking its purpose
3. **Expectation-driven**: Comments include expected outcomes ("should download", "should upload")
4. **Narrative flow**: Comments tell a story, making the test readable without examining code
5. **Concise but informative**: Not verbose, but provides essential context

The pattern follows a typical test structure: **Setup → Action → Assert → Mutate → Action → Assert**, with each phase commented.

### Debugging with Otelite

When OpenTelemetry is enabled (`OpenTelemetry__Enabled=true`), integration tests emit traces and logs to Otelite. Use these commands to debug failed tests:

#### Otelite Database Schema

##### `traces` Table
| Field           | Type     | Sample                 | Description                                                        |
| --------------- | -------- | ---------------------- | ------------------------------------------------------------------ |
| `id`              | INTEGER  | `1`                      | Auto-increment primary key                                         |
| `timestamp`       | DATETIME | `2024-01-15 10:30:00`    | Record insert time                                                 |
| `trace_id`        | TEXT     | `4bf92f3577b34da6`       | W3C trace identifier                                               |
| `span_id`         | TEXT     | `00f067aa0ba902b7`       | Span identifier                                                    |
| `parent_span_id`  | TEXT     | `5b8a5d6c4e3f2a1b`       | Parent span ID (nullable)                                          |
| `service_name`    | TEXT     | `MyMusic.Server`         | Service that created the span                                      |
| `activity_source` | TEXT     | `MyMusic.Server`         | ActivitySource name                                                |
| `span_name`       | TEXT     | `POST /songs`            | Span name (HTTP method+route or operation)                         |
| `kind`            | INTEGER  | `1`                      | Span kind (0=internal, 1=server, 2=client, 3=producer, 4=consumer) |
| `start_time`      | INTEGER  | `1705315800000000000`    | Start time in nanoseconds                                          |
| `end_time`        | INTEGER  | `1705315800100000000`    | End time in nanoseconds                                            |
| `status_code`     | INTEGER  | `0`                      | Status (0=ok, 1=error, 2=error with description)                   |
| `attributes`      | TEXT     | `{"http.method":"POST"}` | JSON-encoded span attributes                                       |
| `raw_json`        | TEXT     | `{...}`                  | Original OTLP JSON payload                                         |

##### `logs` Table
| Field           | Type     | Sample              | Description                                |
| --------------- | -------- | ------------------- | ------------------------------------------ |
| `id`              | INTEGER  | `1`                   | Auto-increment primary key                 |
| `timestamp`       | DATETIME | `2024-01-15 10:30:00` | Record insert time                         |
| `trace_id`        | TEXT     | `4bf92f3577b34da6`    | Associated trace ID (nullable)             |
| `span_id`         | TEXT     | `00f067aa0ba902b7`    | Associated span ID (nullable)              |
| `service_name`    | TEXT     | `MyMusic.Server`      | Service that emitted the log               |
| `severity_number` | INTEGER  | `17`                  | Numeric severity (9=info, 17=error, etc.)  |
| `severity_text`   | TEXT     | `Error`               | Text severity (Info, Warning, Error, etc.) |
| `body`            | TEXT     | `Connection failed`   | Log message body                           |
| `log_timestamp`   | INTEGER  | `1705315800000000000` | Log event time in nanoseconds              |
| `raw_json`        | TEXT     | `{...}`               | Original OTLP JSON payload                 |
| `raw_body`        | TEXT     | `Connection failed`   | Raw log body before processing             |

**Basic Queries:**
```bash
# Full details for specific trace IDs (includes error messages and stack traces)
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT trace_id, span_name, status_code, attributes FROM traces WHERE trace_id IN ('<trace_id_1>', '<trace_id_2>') ORDER BY start_time"

# List recent traces
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT service_name, span_name, trace_id FROM traces ORDER BY id DESC LIMIT 10"

# View recent logs
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT service_name, severity_text, body FROM logs ORDER BY id DESC LIMIT 20"

# Get all spans for a specific trace ID
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT span_name, start_time, end_time FROM traces WHERE trace_id = '<trace_id>' ORDER BY start_time"

# Get integration test logs (includes CLI stdout)
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT body FROM logs WHERE service_name = 'MyMusic.IntegrationTests' ORDER BY id DESC LIMIT 20"
```

**Find Failed Traces (Errors):**
```bash
# Spans with errors (status_code = 2)
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT trace_id, span_name FROM traces WHERE status_code = 2"

# All logs for a failed trace
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT severity_text, body FROM logs WHERE trace_id = '<trace_id>' ORDER BY log_timestamp"
```

**Warning/Error Logs:**
```bash
# All warnings
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT body FROM logs WHERE severity_text = 'Warning' ORDER BY id DESC LIMIT 20"

# Error level logs (severity_number >= 17)
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT body FROM logs WHERE severity_number >= 17"
```

**Text Search in Logs:**
```bash
# Logs containing specific text
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT body FROM logs WHERE body LIKE '%error%' LIMIT 20"
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT body FROM logs WHERE body LIKE '%<keyword>%' LIMIT 20"
```

**Performance Analysis (Slow Spans):**
```bash
# Slowest spans in a trace
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT span_name, (end_time - start_time) as duration_ns FROM traces WHERE trace_id = '<trace_id>' ORDER BY duration_ns DESC LIMIT 10"

# Longest traces overall
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT trace_id, COUNT(*) as span_count, (MAX(end_time) - MIN(start_time)) as duration_ns FROM traces GROUP BY trace_id ORDER BY duration_ns DESC LIMIT 10"
```

**Time-Based Filtering ("What happened after X"):**
```bash
# Traces after a specific timestamp
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT trace_id, span_name, start_time FROM traces WHERE start_time > <timestamp> ORDER BY start_time LIMIT 20"

# Logs after a specific timestamp
# Get integration test logs (includes CLI stdout)
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT service_name, severity_text, body FROM logs WHERE log_timestamp > <timestamp> ORDER BY log_timestamp LIMIT 20"
```

**Correlate Logs with Spans:**
```bash
# All logs for a trace, with their span context
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT l.severity_text, l.body, t.span_name FROM logs l LEFT JOIN traces t ON l.trace_id = t.trace_id AND l.span_id = t.span_id WHERE l.trace_id = '<trace_id>' ORDER BY l.log_timestamp"
```

**Filter by HTTP Route/Method:**
```bash
# All spans for a specific endpoint
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT trace_id, start_time FROM traces WHERE span_name LIKE 'POST%' ORDER BY start_time DESC LIMIT 20"

# All DELETE operations
docker exec my-music-otelite-1 sqlite3 /data/otel.db "SELECT trace_id, span_name, start_time FROM traces WHERE span_name LIKE 'DELETE%' ORDER BY start_time DESC LIMIT 10"
```

**Note on Root Traces:** Root traces are identified by `parent_span_id` being an empty string (`''`). Non-root traces have this field populated with their parent's span ID.

**Find Failed Tests:**
```bash
docker exec my-music-otelite-1 sqlite3 /data/otel.db "
SELECT
  t.trace_id,
  t.timestamp,
  t.span_name,
  json_extract((SELECT value FROM json_each(t.attributes) WHERE json_extract(value, '$.key') = 'test.result'), '$.value.stringValue') as result,
  json_extract((SELECT value FROM json_each(t.attributes) WHERE json_extract(value, '$.key') = 'test.exception.message'), '$.value.stringValue') as error,
  json_extract((SELECT value FROM json_each(t.attributes) WHERE json_extract(value, '$.key') = 'test.exception.stackTrace'), '$.value.stringValue') as stack_trace
FROM traces t
WHERE (t.parent_span_id IS NULL OR t.parent_span_id = '')
  AND t.span_name LIKE 'MyMusic.IntegrationTests%'
  AND json_extract((SELECT value FROM json_each(t.attributes) WHERE json_extract(value, '$.key') = 'test.result'), '$.value.stringValue') != 'passed'
ORDER BY t.timestamp DESC
"
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
- **Always call `.ToUniversalTime()` before saving DateTime values to the DB or to JsonElement data stored in the DB**; always assume UTC when deserializing/parsing DateTime values (Npgsql 10 rejects `DateTimeKind.Local` for `timestamptz` columns)
- Use **Include().ThenInclude()** for related entities; **AsSplitQuery()** for complex queries
- **SongDevice Records:** mark for removal (SyncAction = Remove), never delete
- DTOs organized by resource in `MyMusic.Server/DTO/<Resource>/`; AgileMapper for simple, manual mapping for complex
- **One Operation per Service**: naming pattern `<Resource><Operation>Service` (e.g., `SongEditService`, `SongBatchEditService`, `SongDeleteService`); group in folder/namespace `<ResourcePlural>/` when a resource has many services (e.g., `Songs/SongEditService`, `Songs/SongDeleteService`); define interface in Common, implement in Common/Server, register in DI
- Controllers are thin: parse input, delegate to service, map entities↔DTOs, return output; **all business logic lives in services**
- **ActivitySource naming**: `{ServiceName}` or `{ServiceName}.{Component}` or `{ServiceName}.{Component}.{SubComponent}` (e.g., `MyMusic.CLI`, `MyMusic.Client.Navigation`)

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
- **IntegrationTests** → [docs/development/server.md](docs/development/server.md) and [docs/development/playwright.md](docs/development/playwright.md)
- **Observability** → [docs/development/observability.md](docs/development/observability.md)

These files contain information related to the creating and running automatic tests for each sub-project as well.

## Active Technologies

- .NET 10.0 (backend), TypeScript/React (frontend) + Entity Framework Core, PostgreSQL, TanStack Query, Zustand, Refit (003-metadata-auto-fetch)
- PostgreSQL with EF Core, JsonElement for metadata patch storage (003-metadata-auto-fetch)
- OpenTelemetry with Otelite for distributed tracing and log aggregation (opt-in via `OpenTelemetry__Enabled=true`)

## OpenTelemetry (Observability)

- OpenTelemetry is **opt-in and disabled by default** — set `OpenTelemetry__Enabled=true` to enable
- When enabled, traces and logs are exported to the OTLP endpoint (default: `http://localhost:4317` for gRPC, `http://localhost:4318` for HTTP)
- **Otelite** is available via `docker compose up otelite` or `./tools/otelite server` — lightweight SQLite-based collector (HTTP only, port 4318)
- Use `OpenTelemetry__Protocol=http/protobuf` and `OpenTelemetry__Endpoint=http://localhost:4318` when targeting Otelite (it only supports HTTP, not gRPC)
- **Graceful degradation**: If the OTLP endpoint is unavailable, all components continue operating normally
- Integration tests generate trace IDs and propagate via `traceparent` headers (Playwright, API requests) and `OTEL_TRACE_PARENT` env var (CLI processes)
- For troubleshooting failed tests, enable OpenTelemetry and query Otelite by trace ID
- See [docs/development/observability.md](docs/development/observability.md) for full documentation

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
