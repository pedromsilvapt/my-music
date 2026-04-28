## Why

The CLI project lacks integration tests, making it difficult to verify end-to-end sync functionality. We need automated tests that exercise the full CLI workflow: spawning processes, interacting with the backend API, and validating file system changes.

## What Changes

- Add `CliTestFixture` class to manage test repositories and CLI config files
- Add `CliRunner` static helper to spawn CLI processes and capture output
- Add `FileValidator` static helper to validate file existence and metadata
- Add sample test demonstrating full sync workflow with UI verification
- Extend `CollectionComponent` with editing capabilities for test assertions

## Capabilities

### New Capabilities

- `cli-test-helpers`: Helper classes for spawning CLI processes, managing test repositories, and validating file metadata
- `cli-sync-tests`: Integration tests verifying CLI sync upload and download workflows

### Modified Capabilities

- `collection-component`: Add `EditRowAsync` method to support testing song edits

## Impact

- **New Files**: `Fixtures/CliTestFixture.cs`, `Fixtures/CliRunner.cs`, `Fixtures/CliResult.cs`, `Fixtures/FileValidator.cs`, `Fixtures/FileMetadata.cs`
- **Modified Files**: `Pages/Components/CollectionComponent.cs`
- **New Tests**: `Tests/Cli/CliSyncTests.cs`
- **Dependencies**: Uses existing `IntegrationTestBase`, `TestFiles`, TagLib
