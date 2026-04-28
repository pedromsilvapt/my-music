## ADDED Requirements

### Requirement: GetPendingActionsForDevice recalculates DevicePath using naming template
The server SHALL recalculate `SongDevice.DevicePath` on-the-fly in `GetPendingActionsForDevice` using the device's naming template and the latest song metadata. When the recalculated path differs from the stored `DevicePath`, the pending action SHALL include `PreviousPath` set to the old `DevicePath` and `Path` set to the recalculated path. When paths are identical, `PreviousPath` SHALL be null.

#### Scenario: Path unchanged after metadata edit
- **WHEN** a SongDevice has SyncAction Download and the recalculated DevicePath matches the stored DevicePath
- **THEN** the PendingActionItem Path equals the stored DevicePath
- **AND** PreviousPath is null

#### Scenario: Path changed due to title edit
- **WHEN** a SongDevice has SyncAction Download and the song title has changed
- **THEN** the PendingActionItem Path is the recalculated path reflecting the new title
- **AND** PreviousPath is the old stored DevicePath

#### Scenario: Path changed due to explicit flag change
- **WHEN** a SongDevice has SyncAction Download and the song's explicit flag has changed
- **THEN** the PendingActionItem Path includes or removes "(Explicit)" according to the naming template
- **AND** PreviousPath is the old stored DevicePath

#### Scenario: Path changed due to album or artist change
- **WHEN** a SongDevice has SyncAction Download and the song's album or artists have changed
- **THEN** the PendingActionItem Path reflects the new directory structure per the naming template
- **AND** PreviousPath is the old stored DevicePath

### Requirement: New DevicePath uses GetUniquePath to avoid collisions
When recalculating DevicePath, the server SHALL use the existing `GetUniquePath` logic to ensure the new path does not collide with another SongDevice on the same device.

#### Scenario: New path collides with existing SongDevice
- **WHEN** the recalculated DevicePath matches an existing SongDevice's DevicePath on the same device
- **THEN** the path is made unique by appending " (2)", " (3)", etc.
- **AND** the PendingActionItem Path is the unique path

### Requirement: AcknowledgeAction updates DevicePath when PreviousDevicePath is provided
The `AcknowledgeAction` endpoint SHALL accept a `PreviousDevicePath` field. When provided, it SHALL find the `SongDevice` by `(DeviceId, PreviousDevicePath)` instead of `(DeviceId, DevicePath)`. After finding the record, it SHALL update `DevicePath` to the value in `DevicePath` (the new path) before clearing `SyncAction`.

#### Scenario: Acknowledge with PreviousDevicePath updates DevicePath
- **WHEN** AcknowledgeAction is called with PreviousDevicePath set to the old path and DevicePath set to the new path
- **THEN** the SongDevice is found by (DeviceId, PreviousDevicePath)
- **AND** SongDevice.DevicePath is updated to the new path
- **AND** SyncAction is cleared

#### Scenario: Acknowledge without PreviousDevicePath behaves as before
- **WHEN** AcknowledgeAction is called with PreviousDevicePath null
- **THEN** the SongDevice is found by (DeviceId, DevicePath) as currently
- **AND** DevicePath is not changed
- **AND** SyncAction is cleared

#### Scenario: PreviousDevicePath does not match any SongDevice
- **WHEN** AcknowledgeAction is called with a PreviousDevicePath that does not match any SongDevice for the device
- **THEN** the endpoint throws an exception indicating the SongDevice was not found

### Requirement: SongDevice.DevicePath is not updated preemptively in MarkSongDevicesForDownloadAsync
The `MarkSongDevicesForDownloadAsync` method SHALL continue to only set `SyncAction = Download` without modifying `DevicePath`. DevicePath SHALL only be updated upon successful acknowledgment by the client.

#### Scenario: Metadata change marks devices for download without changing DevicePath
- **WHEN** a song is updated and its checksum changes
- **THEN** MarkSongDevicesForDownloadAsync sets SyncAction to Download on all affected SongDevices
- **AND** DevicePath remains unchanged in the database
