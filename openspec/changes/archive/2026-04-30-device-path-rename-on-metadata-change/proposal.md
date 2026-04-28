## Why

When song metadata changes (title, explicit flag, album, artists), the server correctly relocates its own repository file and marks all `SongDevice` records with `SyncAction = Download`. However, it never recalculates `SongDevice.DevicePath` to reflect the new metadata. This means client devices download the updated file to the **old** path, leaving filenames and folder structures stale. Two integration tests (`Sync_ShouldRenameFileWhenTitleChanges`, `Sync_ShouldRenameFileWhenExplicitFlagChanges`) are explicitly marked as failing due to this gap.

## What Changes

- `GetPendingActionsForDevice` will recalculate each `SongDevice.DevicePath` on-the-fly using the device's naming template and the latest song metadata. If the recalculated path differs from the stored `DevicePath`, the pending action will carry both the new path (as `Path`) and the old path (as `PreviousPath`).
- `PendingActionItem` DTO gains a `PreviousPath` field to communicate the old path to clients.
- `AcknowledgeActionRequest` gains a `PreviousDevicePath` field so the server can locate the `SongDevice` record by its old path and update `DevicePath` to the new value upon acknowledgment.
- CLI `DownloadOneFileAsync` will download to the new path first, then remove the old file (data-safe ordering: write new, then delete old), and clean up empty directories left behind.
- `SongUpdateService.MarkSongDevicesForDownloadAsync` remains unchanged — `DevicePath` is NOT updated preemptively, ensuring DB state always matches actual device state and interrupted syncs remain retryable.

## Capabilities

### New Capabilities
- `device-path-rename`: Recalculates and applies DevicePath changes during sync when song metadata has changed, ensuring device filenames match the current naming template.

### Modified Capabilities
- `sync-phases`: Download actions must handle `PreviousPath` — download to new path, then remove old file and empty directories (applies to both CLI and Mobile)
- `sync-interfaces`: `PendingActionItem` and `AcknowledgeActionRequest` gain new fields for rename communication (applies to Common, CLI, and Mobile)

## Impact

- **Server**: `DevicesController.GetPendingActionsForDevice` and `AcknowledgeAction` — new path calculation and lookup logic
- **Server DTOs**: `PendingActionItem`, `AcknowledgeActionRequest` — new `PreviousPath` / `PreviousDevicePath` fields
- **Common Types**: `PendingActionItem`, `AcknowledgeActionRequest` in `MyMusic.Common/Services/Sync/Types.cs` — new fields
- **CLI**: `Phases.cs` download logic — rename-aware download with safe ordering
- **CLI DTOs**: `PendingActionItem` in CLI API DTOs — new `PreviousPath` field
- **Mobile**: `downloadOneFile` in `atomic-operations.ts` — rename-aware download with safe ordering
- **Mobile API Types**: `PendingActionItemSchema`, `AcknowledgeActionRequestSchema` in `src/api/types.ts` — new `previousPath` / `previousDevicePath` fields
- **Mobile Sync Types**: `PendingActionItem` interface in `src/services/sync/types.ts` — new `previousPath` field
- **Mobile ISyncApiClient**: `acknowledgeAction` method — accept `previousDevicePath` parameter
- **Integration Tests**: Two existing failing tests should now pass; no new test fixtures needed
