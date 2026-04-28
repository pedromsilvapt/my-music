## MODIFIED Requirements

### Requirement: ISyncApiClient interface shared in MyMusic.Common
The system SHALL define `ISyncApiClient` interface in `MyMusic.Common/Services/Sync/` with methods: StartSyncAsync, CheckSyncAsync, UploadFileAsync, RecordChunkAsync, CompleteSyncAsync, GetPendingActionsAsync, AcknowledgeActionAsync, ResolveConflictsAsync, DownloadSongAsync. The `AcknowledgeActionAsync` method SHALL accept `PreviousDevicePath` in the request.

#### Scenario: ISyncApiClient methods match existing API function signatures
- **WHEN** the ISyncApiClient interface is defined
- **THEN** each method signature matches the corresponding API function
- **AND** the production implementation delegates to those same functions

#### Scenario: ISyncApiClient can be replaced with a mock in tests
- **WHEN** a test provides a mock ISyncApiClient
- **THEN** no HTTP calls are made during sync execution
- **AND** test code can verify which API methods were called and with what arguments

#### Scenario: CLI ISyncApiClient delegates to Refit client
- **WHEN** CLI's ISyncApiClient implementation is used
- **THEN** it delegates to the existing IMyMusicClient Refit interface
- **AND** method signatures match the Refit interface

#### Scenario: Mobile ISyncApiClient delegates to fetch functions
- **WHEN** Mobile's ISyncApiClient implementation is used
- **THEN** it delegates to functions in `src/api/sync.ts`
- **AND** method signatures match the fetch functions

#### Scenario: Mobile ISyncApiClient.acknowledgeAction accepts previousDevicePath
- **WHEN** Mobile's ISyncApiClient.acknowledgeAction is called with a previousDevicePath
- **THEN** the request includes previousDevicePath in the body
- **AND** the API call is made to `/devices/{deviceId}/sync/acknowledge`

### Requirement: SyncResult and RecordItem types shared
The system SHALL define `SyncResult` and `RecordItem` types in `MyMusic.Common/Services/Sync/Types.cs` for reuse across CLI and Mobile. `PendingActionItem` SHALL include an optional `PreviousPath` field. `AcknowledgeActionRequest` SHALL include an optional `PreviousDevicePath` field.

#### Scenario: SyncResult has Created, Updated, Skipped, Downloaded, Removed, Failed counters
- **WHEN** a sync completes
- **THEN** SyncResult contains all counter fields
- **AND** both CLI and Mobile use the same type

#### Scenario: RecordItem has Action, Path, Source, Reason, Error fields
- **WHEN** a record is created for upload/download/remove
- **THEN** RecordItem captures all necessary information
- **AND** both CLI and Mobile use the same type

#### Scenario: PendingActionItem includes PreviousPath for rename detection
- **WHEN** a pending action requires a file rename
- **THEN** PendingActionItem.PreviousPath contains the old device path
- **AND** PendingActionItem.Path contains the new device path

#### Scenario: PendingActionItem PreviousPath is null when no rename needed
- **WHEN** a pending action does not require a file rename
- **THEN** PendingActionItem.PreviousPath is null
- **AND** PendingActionItem.Path equals the current DevicePath

#### Scenario: AcknowledgeActionRequest includes PreviousDevicePath for rename
- **WHEN** acknowledging a download that involved a rename
- **THEN** AcknowledgeActionRequest.PreviousDevicePath contains the old path
- **AND** AcknowledgeActionRequest.DevicePath contains the new path
