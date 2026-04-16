## ADDED Requirements

### Requirement: ISyncApiClient interface shared in MyMusic.Common
The system SHALL define `ISyncApiClient` interface in `MyMusic.Common/Services/Sync/` with methods: StartSyncAsync, CheckSyncAsync, UploadFileAsync, RecordChunkAsync, CompleteSyncAsync, GetPendingActionsAsync, AcknowledgeActionAsync, ResolveConflictsAsync, DownloadSongAsync.

#### Scenario: CLI ISyncApiClient delegates to Refit client
- **WHEN** CLI's ISyncApiClient implementation is used
- **THEN** it delegates to the existing IMyMusicClient Refit interface
- **AND** method signatures match the Refit interface

#### Scenario: Mobile ISyncApiClient delegates to fetch functions
- **WHEN** Mobile's ISyncApiClient implementation is used
- **THEN** it delegates to functions in `src/api/sync.ts`
- **AND** method signatures match the fetch functions

### Requirement: ISyncConfig interface shared in MyMusic.Common
The system SHALL define `ISyncConfig` interface in `MyMusic.Common/Services/Sync/` with methods: GetDeviceIdAsync, GetRepositoryPath, GetMusicExtensions, GetExcludePatterns, GetChunkSize, GetLastScanTotal, SetLastScanTotal, SetLastSyncAtAsync.

#### Scenario: CLI ISyncConfig delegates to IOptions<MyMusicOptions>
- **WHEN** CLI's ISyncConfig implementation is used
- **THEN** it reads configuration from IOptions<MyMusicOptions>
- **AND** device ID is retrieved/stored via IMyMusicClient

#### Scenario: Mobile ISyncConfig delegates to configService
- **WHEN** Mobile's ISyncConfig implementation is used
- **THEN** it delegates to functions in `src/services/configService.ts`

### Requirement: ISyncState interface shared in MyMusic.Common
The system SHALL define `ISyncState` interface in `MyMusic.Common/Services/Sync/` with properties: IsCancelled (bool), Options (SyncOptions).

#### Scenario: CLI ISyncState reads from CancellationToken
- **WHEN** CLI's ISyncState.IsCancelled is checked
- **THEN** it returns true if CancellationToken.IsCancellationRequested is true

#### Scenario: Mobile ISyncState reads from Zustand store
- **WHEN** Mobile's ISyncState.IsCancelled is checked
- **THEN** it reads from useSyncStore.getState().isCancelled

### Requirement: IFileOps interface shared in MyMusic.Common
The system SHALL define `IFileOps` interface in `MyMusic.Common/Services/Sync/` with methods: FileExists, EnsureDirectory, WriteFileAsync, DeleteFileAsync, ReadFileBase64Async, GetModificationTimeAsync.

#### Scenario: CLI IFileOps delegates to System.IO.Abstractions
- **WHEN** CLI's IFileOps is used
- **THEN** it delegates to IFileSystem from System.IO.Abstractions
- **AND** all methods are async-compatible

#### Scenario: Mobile IFileOps delegates to expo-file-system
- **WHEN** Mobile's IFileOps is used
- **THEN** it delegates to expo-file-system's File class

### Requirement: IUserPrompt interface shared in MyMusic.Common
The system SHALL define `IUserPrompt` interface in `MyMusic.Common/Services/Sync/` with methods: PromptConflictResolutionAsync (returns ConflictResolution), ConfirmDeletionAsync (returns bool).

#### Scenario: CLI IUserPrompt uses Console.ReadLine
- **WHEN** CLI's IUserPrompt.PromptConflictResolutionAsync is called
- **THEN** it prompts via Console.Write and reads Console.ReadLine
- **AND** user types 'upload', 'download', or 'skip'

#### Scenario: Mobile IUserPrompt uses Alert.alert
- **WHEN** Mobile's IUserPrompt.PromptConflictResolutionAsync is called
- **THEN** it shows an Alert.alert dialog with buttons

### Requirement: IKeepAwake interface with CLI no-op
The system SHALL define `IKeepAwake` interface in `MyMusic.Common/Services/Sync/` with methods: Activate, Deactivate.

#### Scenario: CLI IKeepAwake is no-op
- **WHEN** CLI's IKeepAwake.Activate() is called
- **THEN** no operation is performed
- **AND** no exception is thrown

#### Scenario: Mobile IKeepAwake delegates to expo-keep-awake
- **WHEN** Mobile's IKeepAwake.Activate() is called
- **THEN** it calls activateKeepAwakeAsync from expo-keep-awake

### Requirement: IFileSystemScanner interface for CLI
The system SHALL define `IFileSystemScanner` interface in `MyMusic.CLI/Services/Sync/` (CLI-specific) with method: ScanAsync (returns List<ScannedFile>).

#### Scenario: CLI IFileSystemScanner delegates to IFileScanner
- **WHEN** CLI's IFileSystemScanner.ScanAsync is called
- **THEN** it delegates to the existing IFileScanner from TagLib
- **AND** it returns music files matching configured extensions

### Requirement: SyncOptions includes SyncDirection for CLI
The system SHALL define `SyncOptions` in `MyMusic.Common/Services/Sync/` with properties: Force, DryRun, AutoConfirm, Verbose (CLI-only), SyncDirection (CLI-only), TreatConflictsAsErrors (Mobile-only).

#### Scenario: CLI SyncOptions has SyncDirection
- **WHEN** CLI creates SyncOptions
- **THEN** SyncDirection is set based on command-line arguments
- **AND** Verbose is set based on --verbose flag

#### Scenario: Mobile SyncOptions has TreatConflictsAsErrors
- **WHEN** Mobile creates SyncOptions
- **THEN** TreatConflictsAsErrors is set from user preferences
- **AND** SyncDirection defaults to Both (not used by Mobile)

### Requirement: SyncResult and RecordItem types shared
The system SHALL define `SyncResult` and `RecordItem` types in `MyMusic.Common/Services/Sync/Types.cs` for reuse across CLI and Mobile.

#### Scenario: SyncResult has Created, Updated, Skipped, Downloaded, Removed, Failed counters
- **WHEN** a sync completes
- **THEN** SyncResult contains all counter fields
- **AND** both CLI and Mobile use the same type

#### Scenario: RecordItem has Action, Path, Source, Reason, Error fields
- **WHEN** a record is created for upload/download/remove
- **THEN** RecordItem captures all necessary information
- **AND** both CLI and Mobile use the same type
