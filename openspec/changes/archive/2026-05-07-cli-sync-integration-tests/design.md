## Context

The CLI sync integration test suite (`CliSyncTests`) currently has 8 tests covering basic upload/download round-trips, idempotency, local deletion, metadata changes, and file rename. The CLI already supports `--direction`, `--dry-run`, and `--force` flags, but none of these are exercised in integration tests. Additionally, server-initiated removal, conflict resolution, multi-song scenarios, and sequential sync behavior are untested.

## Goals / Non-Goals

**Goals:**
- Add 18 new integration tests organized in partial class files by category
- Validate sync direction flags (`--direction up`/`down`) work correctly
- Validate dry-run mode reports changes without executing them
- Validate force mode re-uploads unchanged files
- Validate server-initiated song removal (both mark-for-removal and delete flows)
- Validate conflict detection and auto-resolution
- Validate multi-song upload/download/remove in a single sync
- Validate metadata edge cases (album/artist rename causing file rename)
- Validate sequential sync state transitions across multiple syncs
- Extend `CliRunner.SyncAsync` to accept a `direction` parameter

**Non-Goals:**
- Changes to production code (CLI or server)
- Changes to existing test infrastructure beyond `CliRunner` direction support
- Performance or load testing
- Network error or timeout scenarios

## Decisions

### Partial class file organization
Tests are organized into partial class files by category to keep the main `CliSyncTests.cs` unchanged and each category self-contained:
- `CliSyncTests.Direction.cs` — sync direction (up/down)
- `CliSyncTests.DryRun.cs` — dry-run mode
- `CliSyncTests.Force.cs` — force mode
- `CliSyncTests.ServerRemoval.cs` — server-initiated removal
- `CliSyncTests.Conflicts.cs` — conflict resolution
- `CliSyncTests.MultiSong.cs` — multi-song combinations
- `CliSyncTests.MetadataEdgeCases.cs` — album/artist renames
- `CliSyncTests.Sequential.cs` — sequential syncs

This avoids a single massive file while sharing the same test class setup (fixtures, initialization, disposal).

### CliRunner direction parameter
Add a `SyncDirection?` parameter to `CliRunner.SyncAsync` that maps to `--direction up`/`--direction down` CLI args. This mirrors the existing `force` and `dryRun` pattern.

### No new fixtures needed
All tests reuse existing fixtures: `CliTestFixture`, `SongsFixture`, `ManageSongDevicesFlow`, `EditSongFlow`, `ShouldSongExistInDeviceFlow`, `FileValidator`, `CliRunner`. Server-side song deletion uses the `RequestContext` API directly (DELETE `/api/songs/{id}`) which is already available via `IntegrationTestBase`.

### Test style
Follow the existing pattern: intent-focused comments, concise steps, Shouldly assertions, and Flows for UI interactions. Each test is self-contained with its own setup/data.

## Risks / Trade-offs

- **Test runtime**: 18 new integration tests will add to CI time. Mitigation: tests are independent and could be parallelized by the test runner.
- **CLI direction flag format**: `SyncDirection` enum in `CliRunner` must match the CLI's `SyncDirection` enum values (`Up`, `Down`, `Both`). If these diverge, tests will fail. Mitigation: use string literal mapping (`"up"`, `"down"`) instead of referencing the CLI enum directly, since the test project shouldn't depend on CLI internals.
- **Server song deletion API**: Tests that delete songs from the server use `DELETE /api/songs/{id}`. If this endpoint changes, tests break. Mitigation: use the existing `RequestContext` from `IntegrationTestBase`.
- **Partial class compilation**: All partial class files must share the same namespace and class name. Mitigation: follow consistent naming and namespace conventions.