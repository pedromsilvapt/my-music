## 1. CLI Test Helpers Infrastructure

- [x] 1.1 Add project reference from IntegrationTests to CLI project
- [x] 1.2 Create `CliResult` record to capture process output (exit code, stdout, stderr)
- [x] 1.3 Create `CliRunner` static class with `RunAsync` and `SyncAsync` methods
- [x] 1.4 Create `FileMetadata` record for MP3 metadata (title, album, artists, etc.)
- [x] 1.5 Create `FileValidator` static class with `GetMetadataAsync` and `AssertMetadataAsync` methods

## 2. CLI Test Fixture

- [x] 2.1 Create `CliTestFixture` class implementing `IAsyncDisposable`
- [x] 2.2 Implement temp directory creation and cleanup
- [x] 2.3 Implement CLI config file generation with test settings
- [x] 2.4 Add `CreateSongAsync` method to create MP3 files in repository
- [x] 2.5 Add device creation via API during initialization

## 3. Songs Collection Component

- [x] 3.1 Create `SongsCollectionComponent` inheriting from `CollectionComponent`
- [x] 3.2 Add helper method to get cell value by row and column
- [x] 3.3 Add helper method to get song ID by row
- [x] 3.4 Update `SongsPage` to use `SongsCollectionComponent`

## 4. Sample Integration Test

- [x] 4.1 Create `Tests/Cli/CliSyncTests.cs` test class
- [x] 4.2 Implement test: setup file → sync → verify UI → edit → sync → validate file
- [x] 4.3 Verify test passes with `dotnet test --filter "CliSyncTests"`
