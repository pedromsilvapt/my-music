## Context

When song metadata changes on the server (title, explicit flag, album, artists), the server correctly relocates its own `RepositoryPath` via `FileTarget.Relocate()` in `SongUpdateService`. However, `SongDevice.DevicePath` — the relative path on each device — is never recalculated. The `MarkSongDevicesForDownloadAsync` method only sets `SyncAction = Download`, leaving `DevicePath` pointing to the old location. Clients then download the updated file to the stale path.

The `SongDevice` entity has a unique index on `(DeviceId, DevicePath)`, which constrains how we can update paths. The naming template system (`TemplateNamingStrategy` + `Device.NamingTemplate`) already exists and is used correctly when songs are first added to devices (`MusicService.AddSongsToDevice`).

Two integration tests are explicitly marked as failing due to this gap:
- `Sync_ShouldRenameFileWhenTitleChanges` (CliSyncTests.cs:221)
- `Sync_ShouldRenameFileWhenExplicitFlagChanges` (CliSyncTests.cs:253)

## Goals / Non-Goals

**Goals:**
- Recalculate `SongDevice.DevicePath` using the device's naming template when metadata changes affect the path
- Communicate both old and new paths to clients during sync so they can safely rename local files
- Ensure data safety: download new file first, remove old file second
- Keep DB state consistent with actual device state — `DevicePath` in DB only updates after the client acknowledges
- Make the same API contract usable by both CLI and Mobile clients
- Implement the rename-aware download logic in both CLI and Mobile clients

**Non-Goals:**
- Handling device naming template changes (when a user changes the device's `NamingTemplate` setting — that's a separate concern requiring bulk path recalculation)
- Changing the `SongUpdateService.MarkSongDevicesForDownloadAsync` method (it continues to only set `SyncAction = Download`)
- Adding new integration test fixtures (existing failing tests should pass once implementation is correct)

## Decisions

### D1: Recalculate DevicePath on-the-fly in GetPendingActionsForDevice (not in MarkSongDevicesForDownloadAsync)

**Choice**: Calculate the new path at query time in `GetPendingActionsForDevice`, not at update time.

**Alternatives considered**:
- **Update DevicePath in MarkSongDevicesForDownloadAsync**: Would change DB state before the client has processed the action. If sync is interrupted, DB has new path but device still has old file. Breaks retry. Also risks unique index collision if new path conflicts with another SongDevice on same device.
- **Client-side calculation**: CLI doesn't currently know the naming template. Would require duplicating naming logic in every client. Race conditions between client and server on what the "correct" path is.

**Rationale**: On-the-fly calculation means DB always reflects reality. If sync fails midway, next sync recalculates the same result. No migration needed on `SongDevice` entity.

### D2: Use PreviousPath / PreviousDevicePath fields to communicate rename

**Choice**: Add `PreviousPath` to `PendingActionItem` and `PreviousDevicePath` to `AcknowledgeActionRequest`.

**Alternatives considered**:
- **Only send new path, client infers old path from scan**: Requires client to match files by SongId, adding complexity and race conditions.
- **Two separate actions (Remove old + Download new)**: Server would need to create two `SongDevice` records or split one action into two. Breaks the unique index constraint and complicates acknowledge logic.

**Rationale**: Explicit old+new path is clear, atomic, and handles the unique index lookup correctly. The acknowledge endpoint can find the `SongDevice` by `(DeviceId, PreviousDevicePath)` and update `DevicePath` to the new value.

### D3: AcknowledgeAction finds SongDevice by PreviousDevicePath, updates DevicePath

**Choice**: When `PreviousDevicePath` is provided in the acknowledge request, find the `SongDevice` by the old path, then update `DevicePath` to the new path.

**Rationale**: This is the only safe time to update `DevicePath` — the client has confirmed the file is at the new location. The unique index `(DeviceId, DevicePath)` is satisfied because the old row still has the old `DevicePath` at lookup time; we update it atomically in the same `SaveChangesAsync` call.

### D4: Download-first, remove-later ordering in clients (CLI and Mobile)

**Choice**: Both CLI and Mobile download the new file to the new path first, then remove the old file from the old path, then clean empty directories.

**Rationale**: If download fails, old file remains intact. If download succeeds but delete fails, user has both files (not ideal but no data loss). This matches the user's explicit requirement for data safety. Both clients use the same safe ordering pattern.

### D5: Mobile uses the same API contract and patterns as CLI

**Choice**: Mobile's `downloadOneFile` function follows the same pattern as CLI's `DownloadOneFileAsync` — accept `previousPath` parameter, download to new path first, delete old file after, clean empty directories.

**Alternatives considered**:
- **Different implementation approach for Mobile**: Would create inconsistency and maintenance burden. The same logic applies regardless of platform.

**Rationale**: Both CLI and Mobile share the same sync architecture (phases, atomic operations). The rename logic is identical — only the file system APIs differ (System.IO vs expo-file-system).

## Risks / Trade-offs

- **[New path collides with existing file on device]** → The server-side `GetPendingActionsForDevice` needs to use `GetUniquePath` logic (already exists in `SongsController`) to avoid generating a path that conflicts with another `SongDevice` on the same device. This adds query complexity but is necessary.

- **[Multiple metadata changes between syncs]** → On-the-fly calculation naturally handles this — latest metadata always produces latest path. No intermediate state is persisted.

- **[Interrupted sync leaves orphaned old file]** → The old file at `PreviousPath` remains on disk if the sync is interrupted after download but before delete. Next sync scan will detect the orphaned file (it won't match any `SongDevice`). This is a pre-existing gap (orphaned files from failed syncs are not cleaned up) and is out of scope for this change.

- **[Empty directory cleanup after rename]** → When a rename moves a file out of a directory (e.g., artist or album name changes), the parent directories may become empty. The CLI should clean these up to avoid cluttering the filesystem.

- **[GetPendingActionsForDevice performance]** → The new implementation needs to load `SongDevice.Song` with related entities (Album.Artist, Artists) and the `Device.NamingTemplate` for each pending action. This adds queries compared to the current simple projection. The volume of pending actions is typically small (< 100), so this should be acceptable.
