## ADDED Requirements

### Requirement: ISyncApiClient interface shared in MyMusic.Common
The system SHALL define `ISyncApiClient` interface in `MyMusic.Common/Services/Sync/` with methods: StartSyncAsync, CheckSyncAsync, UploadFileAsync, RecordChunkAsync, CompleteSyncAsync, GetPendingActionsAsync, AcknowledgeActionAsync, ResolveConflictsAsync, DownloadSongAsync.

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

### Requirement: ISyncConfig interface for configuration access
The system SHALL define an `ISyncConfig` interface that abstracts access to sync-relevant configuration. The interface SHALL expose: `getDeviceId`, `getRepositoryPath`, `getMusicExtensions`, `getExcludePatterns`, `getChunkSize`, `getLastScanTotal`, `setLastScanTotal`, `setLastSyncAt`. The production implementation SHALL delegate to `configService` (Mobile) or `IOptions<MyMusicOptions>` (CLI).

#### Scenario: ISyncConfig methods match configService signatures
- **WHEN** the ISyncConfig interface is defined
- **THEN** each method signature matches the corresponding configService function
- **AND** the production implementation returns the same values as calling configService directly

#### Scenario: CLI ISyncConfig delegates to IOptions<MyMusicOptions>
- **WHEN** CLI's ISyncConfig implementation is used
- **THEN** it reads configuration from IOptions<MyMusicOptions>
- **AND** device ID is retrieved/stored via IMyMusicClient

#### Scenario: Mobile ISyncConfig delegates to configService
- **WHEN** Mobile's ISyncConfig implementation is used
- **THEN** it delegates to functions in `src/services/configService.ts`

### Requirement: ISyncState interface for cancellation and options
The system SHALL define an `ISyncState` interface that abstracts access to the sync state. The interface SHALL expose: `isCancelled` (readonly boolean), `options` (readonly sync options: force, dryRun, autoConfirm, treatConflictsAsErrors, scannerType). The production implementation SHALL read from `useSyncStore.getState()` (Mobile) or `CancellationToken` (CLI).

#### Scenario: ISyncState reads cancellation state
- **WHEN** the sync loop checks `state.isCancelled`
- **THEN** it returns the current value from the sync state source

#### Scenario: ISyncState reads sync options
- **WHEN** a phase function accesses `state.options`
- **THEN** it returns the options that were set when sync was started

#### Scenario: CLI ISyncState reads from CancellationToken
- **WHEN** CLI's ISyncState.IsCancelled is checked
- **THEN** it returns true if CancellationToken.IsCancellationRequested is true

#### Scenario: Mobile ISyncState reads from Zustand store
- **WHEN** Mobile's ISyncState.IsCancelled is checked
- **THEN** it reads from useSyncStore.getState().isCancelled

### Requirement: IFileSystemScanner interface for file scanning
The system SHALL define an `IFileSystemScanner` interface matching the existing `ScannerFunction` type from `scannerRegistry.ts` (Mobile) or `IFileScanner` from TagLib (CLI). The interface SHALL accept `directoryUri` and `ScanOptions`, and return `Promise<ScanResult>`.

#### Scenario: IFileSystemScanner matches existing scanner signature
- **WHEN** the IFileSystemScanner interface is defined
- **THEN** both `scanFromDirectory` (fileSystem) and `scanFromDirectory` (mediaLibrary) structurally satisfy the interface
- **AND** `scannerRegistry` lookups can be used as the production implementation

#### Scenario: CLI IFileSystemScanner delegates to IFileScanner
- **WHEN** CLI's IFileSystemScanner.ScanAsync is called
- **THEN** it delegates to the existing IFileScanner from TagLib
- **AND** it returns music files matching configured extensions

### Requirement: IFileOps interface for local file operations
The system SHALL define an `IFileOps` interface that abstracts all local file system operations performed during sync. The interface SHALL expose: `fileExists(path)`, `ensureDirectory(path)`, `writeFile(path, data)`, `deleteFile(path)`, `readFileBase64(path)`, `getModificationTime(path)`. The production implementation SHALL use `expo-file-system`'s `File` class (Mobile) or `System.IO.Abstractions` (CLI).

#### Scenario: IFileOps can be mocked to avoid file system access in tests
- **WHEN** a test provides a mock IFileOps
- **THEN** no actual file system reads, writes, or deletes occur during sync execution

#### Scenario: IFileOps production implementation uses expo-file-system
- **WHEN** the production IFileOps is used (Mobile)
- **THEN** `fileExists` delegates to `new File(toFileUri(path)).exists`
- **AND** `writeFile` delegates to `new File(toFileUri(path)).write(data)`
- **AND** `deleteFile` delegates to `new File(toFileUri(path)).delete()`
- **AND** `readFileBase64` delegates to `new File(toFileUri(path)).base64()`

#### Scenario: CLI IFileOps delegates to System.IO.Abstractions
- **WHEN** CLI's IFileOps is used
- **THEN** it delegates to IFileSystem from System.IO.Abstractions
- **AND** all methods are async-compatible

### Requirement: IKeepAwake interface for screen wake lock
The system SHALL define an `IKeepAwake` interface with two methods: `activate()` and `deactivate()`. The production implementation SHALL delegate to `expo-keep-awake`'s `activateKeepAwakeAsync` and `deactivateKeepAwake` (Mobile) or be a no-op (CLI).

#### Scenario: IKeepAwake activate is called on sync start
- **WHEN** a sync begins
- **THEN** `keepAwake.activate()` is called
- **AND** the screen stays awake during the sync (Mobile) or no operation is performed (CLI)

#### Scenario: IKeepAwake deactivate is always called on sync end
- **WHEN** a sync completes, errors, or is cancelled
- **THEN** `keepAwake.deactivate()` is called (in a finally block)

#### Scenario: CLI IKeepAwake is no-op
- **WHEN** CLI's IKeepAwake.Activate() is called
- **THEN** no operation is performed
- **AND** no exception is thrown

#### Scenario: Mobile IKeepAwake delegates to expo-keep-awake
- **WHEN** Mobile's IKeepAwake.Activate() is called
- **THEN** it calls activateKeepAwakeAsync from expo-keep-awake

### Requirement: IUserPrompt interface for user interaction
The system SHALL define an `IUserPrompt` interface with two methods: `promptConflictResolution(filePath)` returning `Promise<ConflictResolution>` and `confirmDeletion(filePath)` returning `Promise<boolean>`. The production implementation SHALL use React Native `Alert.alert` (Mobile) or `Console.ReadLine` (CLI).

#### Scenario: IUserPrompt prompts for conflict resolution
- **WHEN** a conflict cannot be auto-resolved
- **THEN** `userPrompt.promptConflictResolution(path)` is called
- **AND** the user can choose 'upload', 'download', or 'skip'

#### Scenario: IUserPrompt confirms file deletion
- **WHEN** a server-initiated Remove action is encountered and autoConfirm is false
- **THEN** `userPrompt.confirmDeletion(path)` is called
- **AND** the user can confirm or cancel deletion

#### Scenario: CLI IUserPrompt uses Console.ReadLine
- **WHEN** CLI's IUserPrompt.PromptConflictResolutionAsync is called
- **THEN** it prompts via Console.Write and reads Console.ReadLine
- **AND** user types 'upload', 'download', or 'skip'

#### Scenario: Mobile IUserPrompt uses Alert.alert
- **WHEN** Mobile's IUserPrompt.PromptConflictResolutionAsync is called
- **THEN** it shows an Alert.alert dialog with buttons

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