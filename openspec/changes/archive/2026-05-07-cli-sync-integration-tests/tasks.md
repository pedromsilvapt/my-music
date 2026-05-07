## 1. Infrastructure Updates

- [x] 1.1 Add `SyncDirection` enum to `CliRunner.cs` (or `CliTestFixture.cs`) with values `Up`, `Down`, `Both`
- [x] 1.2 Add `direction` parameter to `CliRunner.SyncAsync` that maps `SyncDirection` to `--direction up`/`--direction down` CLI args
- [x] 1.3 Verify `ManageSongDevicesFlow` supports `"Remove"` action for marking device removal (check existing code, add if missing)
- [x] 1.4 Add a helper method to delete a server song via API (`DELETE /api/songs/{id}`) using `RequestContext`, if not already available

## 2. Sync Direction Tests

- [x] 2.1 Create `CliSyncTests.Direction.cs` partial class file in `Tests/Cli/`
- [x] 2.2 Implement `Sync_WithDirectionUp_ShouldUploadWithoutDownloading` test
- [x] 2.3 Implement `Sync_WithDirectionDown_ShouldDownloadWithoutUploading` test

## 3. Dry-Run Mode Tests

- [x] 3.1 Create `CliSyncTests.DryRun.cs` partial class file in `Tests/Cli/`
- [x] 3.2 Implement `Sync_DryRun_ShouldReportCreationWithoutUploading` test
- [x] 3.3 Implement `Sync_DryRun_ShouldReportDownloadWithoutDownloading` test

## 4. Force Mode Tests

- [x] 4.1 Create `CliSyncTests.Force.cs` partial class file in `Tests/Cli/`
- [x] 4.2 Implement `Sync_WithForce_ShouldReUploadUnchangedFiles` test

## 5. Server-Initiated Remove Tests

- [x] 5.1 Create `CliSyncTests.ServerRemoval.cs` partial class file in `Tests/Cli/`
- [x] 5.2 Implement `Sync_ShouldDeleteLocalFileWhenServerMarksForRemoval` test
- [x] 5.3 Implement `Sync_ShouldRemoveLocalFileWhenServerSongDeleted` test

## 6. Conflict Resolution Tests

- [x] 6.1 Create `CliSyncTests.Conflicts.cs` partial class file in `Tests/Cli/`
- [x] 6.2 Implement `Sync_ConflictResolution_ShouldAutoResolveWhenContentIdentical` test
- [x] 6.3 Implement `Sync_ConflictResolution_ShouldReportTrueConflictWhenContentDiffers` test

## 7. Multi-Song Combination Tests

- [x] 7.1 Create `CliSyncTests.MultiSong.cs` partial class file in `Tests/Cli/`
- [x] 7.2 Implement `Sync_ShouldHandleMultipleSimultaneousUploads` test
- [x] 7.3 Implement `Sync_ShouldHandleMixedOperationsInOneSync` test
- [x] 7.4 Implement `Sync_ShouldHandleMultipleServerDownloadsInOneSync` test
- [x] 7.5 Implement `Sync_ShouldHandleMultipleServerRemovesInOneSync` test

## 8. Metadata Edge Case Tests

- [x] 8.1 Create `CliSyncTests.MetadataEdgeCases.cs` partial class file in `Tests/Cli/`
- [x] 8.2 Implement `Sync_ShouldRenameFileWhenAlbumChanges` test
- [x] 8.3 Implement `Sync_ShouldRenameFileWhenArtistChanges` test

## 9. Sequential Sync Tests

- [x] 9.1 Create `CliSyncTests.Sequential.cs` partial class file in `Tests/Cli/`
- [x] 9.2 Implement `Sync_ShouldHandleSuccessiveTitleChangesAcrossSyncs` test
- [x] 9.3 Implement `Sync_ShouldUploadAndUpdateAndThenDownloadInSuccessiveSyncs` test

## 10. Verification

- [ ] 10.1 Run `dotnet test MyMusic.IntegrationTests` and verify all new tests pass
- [ ] 10.2 Verify existing tests still pass with no regressions