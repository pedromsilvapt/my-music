## 1. Shared Types & DTOs

- [x] 1.1 Add `PreviousPath` property to `PendingActionItem` in `MyMusic.Common/Services/Sync/Types.cs`
- [x] 1.2 Add `PreviousDevicePath` property to `AcknowledgeActionRequest` in `MyMusic.Common/Services/Sync/Types.cs`
- [x] 1.3 Add `PreviousPath` property to `PendingActionItem` in `MyMusic.Server/DTO/Sync/GetPendingActionsResponse.cs`
- [x] 1.4 Add `PreviousDevicePath` property to `AcknowledgeActionRequest` in `MyMusic.Server/DTO/Sync/AcknowledgeActionRequest.cs`
- [x] 1.5 Add `PreviousPath` property to `PendingActionItem` in `MyMusic.CLI/Api/Dtos/GetPendingActionsResponse.cs`

## 2. Server-Side Path Recalculation

- [x] 2.1 Refactor `GetPendingActionsForDevice` in `DevicesController.cs` to eagerly load `SongDevice.Song` with related entities (Album.Artist, Artists.Artist) and `Device.NamingTemplate`
- [x] 2.2 For each SongDevice with SyncAction Download, recalculate the DevicePath using the device's naming template via `TemplateNamingStrategy`
- [x] 2.3 When recalculated path differs from stored DevicePath, set PendingActionItem.Path to the new path and PreviousPath to the stored DevicePath
- [x] 2.4 When recalculated path matches stored DevicePath, set PendingActionItem.Path to DevicePath and PreviousPath to null
- [x] 2.5 Apply `GetUniquePath` logic to avoid DevicePath collisions with existing SongDevices on the same device

## 3. Server-Side Acknowledge Update

- [x] 3.1 Modify `AcknowledgeAction` in `DevicesController.cs` to find SongDevice by `(DeviceId, PreviousDevicePath)` when `PreviousDevicePath` is provided, falling back to `(DeviceId, DevicePath)` when null
- [x] 3.2 When `PreviousDevicePath` is provided and SongDevice is found, update `SongDevice.DevicePath` to the value of `DevicePath` (the new path) before clearing SyncAction

## 4. CLI Download Rename Logic

- [x] 4.1 Modify `DownloadOneFileAsync` in `Phases.cs` to accept an optional `previousPath` parameter
- [x] 4.2 Download to new path first (existing behavior), then if `previousPath` is non-null and old file exists, delete the old file after successful download
- [x] 4.3 Clean up empty parent directories left by the old file after deletion
- [x] 4.4 Include `PreviousDevicePath` in the `AcknowledgeActionRequest` when `previousPath` is provided
- [x] 4.5 Pass `PreviousPath` from `PendingActionItem` through `ServerActionsPhaseAsync` to `DownloadOneFileAsync`

## 5. CLI Defaults & API Client

- [x] 5.1 Update `AcknowledgeActionAsync` call in `Defaults.cs` to include `PreviousDevicePath` when provided
- [x] 5.2 Update CLI API client mapping to include `PreviousPath` from server response

## 6. Integration Tests

- [x] 6.1 Remove "Test failing, ignore for now" comments from `Sync_ShouldRenameFileWhenTitleChanges` and `Sync_ShouldRenameFileWhenExplicitFlagChanges` in `CliSyncTests.cs`
- [x] 6.2 Run both rename integration tests and verify they pass
- [x] 6.3 Run the full CLI integration test suite to ensure no regressions

## 7. Mobile API Types

- [x] 7.1 Add `previousPath` property to `PendingActionItemSchema` in `MyMusic.Mobile/src/api/types.ts`
- [x] 7.2 Add `previousDevicePath` property to `AcknowledgeActionRequestSchema` in `MyMusic.Mobile/src/api/types.ts`

## 8. Mobile Sync Types

- [x] 8.1 Add `previousPath` property to `PendingActionItem` interface in `MyMusic.Mobile/src/services/sync/types.ts`
- [x] 8.2 Update `ISyncApiClient.acknowledgeAction` signature to accept `previousDevicePath` in request parameter

## 9. Mobile Download Rename Logic

- [x] 9.1 Modify `downloadOneFile` in `atomic-operations.ts` to accept an optional `previousPath` parameter
- [x] 9.2 Download to new path first (existing behavior), then if `previousPath` is non-null and old file exists, delete the old file after successful download
- [x] 9.3 Clean up empty parent directories left by the old file after deletion
- [x] 9.4 Include `previousDevicePath` in the `acknowledgeAction` request when `previousPath` is provided
- [x] 9.5 Pass `previousPath` from `PendingActionItem` through `serverActionsPhase` to `downloadOneFile`

## 10. Mobile Defaults & API Client

- [x] 10.1 Update `createDefaultApiClient` in `defaults.ts` to pass `previousDevicePath` to `acknowledgeAction` when provided
- [x] 10.2 Ensure `IFileOps` interface supports directory cleanup (may need to add `deleteEmptyDirectories` method or use existing `deleteFile` + manual cleanup)
