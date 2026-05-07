## Why

The existing 8 CLI sync integration tests cover basic upload/download round-trips, idempotency, local deletion, and metadata changes — but they don't exercise sync direction flags (`--direction`), dry-run mode (`--dry-run`), force mode (`--force`), server-initiated removals, conflict resolution, multi-song combinations, metadata edge cases (album/artist renames), or sequential sync scenarios. These gaps leave critical sync behaviors unverified at the integration level.

## What Changes

- Add new partial class test files: `CliSyncTests.Direction.cs` (2 tests), `CliSyncTests.DryRun.cs` (2 tests), `CliSyncTests.Force.cs` (1 test), `CliSyncTests.Conflicts.cs` (2 tests), plus tests for server-initiated removal, multi-song combos, metadata edge cases, and sequential syncs across additional partial class files
- 18 new test scenarios total across categories: sync direction, dry-run, force, server-initiated removal, conflict resolution, multi-song, metadata edge cases, and sequential syncs
- Reuse existing fixtures (`CliTestFixture`, `SongsFixture`, `CliRunner`, `FileValidator`, `ManageSongDevicesFlow`) and page objects
- Extend `CliRunner` and `ManageSongDevicesFlow` if needed to support `--direction`, `--dry-run`, `--force` flags and "Remove" sync action

## Capabilities

### New Capabilities
- `cli-sync-advanced-tests`: Integration tests for sync direction, dry-run, force mode, server-initiated removal, conflict resolution, multi-song combinations, metadata edge cases, and sequential sync scenarios

### Modified Capabilities
- `cli-sync-tests`: Adding cross-references to new advanced test scenarios and updating coverage expectations

## Impact

- **Test files**: New partial class files under `MyMusic.IntegrationTests/Tests/Cli/`
- **Test infrastructure**: Possible updates to `CliRunner` (direction/dry-run/force params already exist), `ManageSongDevicesFlow` (may need "Remove" action support), and `SongsFixture` (may need server-side song deletion)
- **CI**: Test count increases by ~18; CI pipeline runtime impact should be minimal since these are integration tests that reuse the existing browser fixture
- **No production code changes**: Only test code is affected