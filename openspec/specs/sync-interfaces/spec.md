## ADDED Requirements

### Requirement: ISyncApiClient interface for server communication
The system SHALL define an `ISyncApiClient` interface that abstracts all server HTTP calls used during sync. The interface SHALL expose methods: `startSync`, `checkSync`, `uploadFile`, `recordChunk`, `completeSync`, `getPendingActions`, `acknowledgeAction`, `resolveConflicts`, `downloadSong`. Each method SHALL have the same signature as the corresponding function in `src/api/sync.ts`.

#### Scenario: ISyncApiClient methods match existing API function signatures
- **WHEN** the ISyncApiClient interface is defined
- **THEN** each method signature exactly matches the corresponding exported function in `src/api/sync.ts`
- **AND** the production implementation delegates to those same functions

#### Scenario: ISyncApiClient can be replaced with a mock in tests
- **WHEN** a test provides a mock ISyncApiClient
- **THEN** no HTTP calls are made during sync execution
- **AND** test code can verify which API methods were called and with what arguments

### Requirement: ISyncConfig interface for configuration access
The system SHALL define an `ISyncConfig` interface that abstracts access to sync-relevant configuration. The interface SHALL expose: `getDeviceId`, `getRepositoryPath`, `getMusicExtensions`, `getExcludePatterns`, `getChunkSize`, `getLastScanTotal`, `setLastScanTotal`, `setLastSyncAt`. The production implementation SHALL delegate to `configService`.

#### Scenario: ISyncConfig methods match configService signatures
- **WHEN** the ISyncConfig interface is defined
- **THEN** each method signature matches the corresponding configService function
- **AND** the production implementation returns the same values as calling configService directly

### Requirement: ISyncState interface for cancellation and options
The system SHALL define an `ISyncState` interface that abstracts access to the sync Zustand store. The interface SHALL expose: `isCancelled` (readonly boolean), `options` (readonly sync options: force, dryRun, autoConfirm, treatConflictsAsErrors, scannerType). The production implementation SHALL read from `useSyncStore.getState()`.

#### Scenario: ISyncState reads cancellation state
- **WHEN** the sync loop checks `state.isCancelled`
- **THEN** it returns the current value from the Zustand syncStore

#### Scenario: ISyncState reads sync options
- **WHEN** a phase function accesses `state.options`
- **THEN** it returns the options that were set when `startSync` was called on the store

### Requirement: IFileSystemScanner interface for file scanning
The system SHALL define an `IFileSystemScanner` interface matching the existing `ScannerFunction` type from `scannerRegistry.ts`. The interface SHALL accept `directoryUri` and `ScanOptions`, and return `Promise<ScanResult>`.

#### Scenario: IFileSystemScanner matches existing scanner signature
- **WHEN** the IFileSystemScanner interface is defined
- **THEN** both `scanFromDirectory` (fileSystem) and `scanFromDirectory` (mediaLibrary) structurally satisfy the interface
- **AND** `scannerRegistry` lookups can be used as the production implementation

### Requirement: IFileOps interface for local file operations
The system SHALL define an `IFileOps` interface that abstracts all local file system operations performed during sync. The interface SHALL expose: `fileExists(path)`, `ensureDirectory(path)`, `writeFile(path, data)`, `deleteFile(path)`, `readFileBase64(path)`, `getModificationTime(path)`. The production implementation SHALL use `expo-file-system`'s `File` class.

#### Scenario: IFileOps can be mocked to avoid file system access in tests
- **WHEN** a test provides a mock IFileOps
- **THEN** no actual file system reads, writes, or deletes occur during sync execution

#### Scenario: IFileOps production implementation uses expo-file-system
- **WHEN** the production IFileOps is used
- **THEN** `fileExists` delegates to `new File(toFileUri(path)).exists`
- **AND** `writeFile` delegates to `new File(toFileUri(path)).write(data)`
- **AND** `deleteFile` delegates to `new File(toFileUri(path)).delete()`
- **AND** `readFileBase64` delegates to `new File(toFileUri(path)).base64()`

### Requirement: IKeepAwake interface for screen wake lock
The system SHALL define an `IKeepAwake` interface with two methods: `activate()` and `deactivate()`. The production implementation SHALL delegate to `expo-keep-awake`'s `activateKeepAwakeAsync` and `deactivateKeepAwake`.

#### Scenario: IKeepAwake activate is called on sync start
- **WHEN** a sync begins
- **THEN** `keepAwake.activate()` is called
- **AND** the screen stays awake during the sync

#### Scenario: IKeepAwake deactivate is always called on sync end
- **WHEN** a sync completes, errors, or is cancelled
- **THEN** `keepAwake.deactivate()` is called (in a finally block)

### Requirement: IUserPrompt interface for user interaction
The system SHALL define an `IUserPrompt` interface with two methods: `promptConflictResolution(filePath)` returning `Promise<ConflictResolution>` and `confirmDeletion(filePath)` returning `Promise<boolean>`. The production implementation SHALL use React Native `Alert.alert`.

#### Scenario: IUserPrompt prompts for conflict resolution
- **WHEN** a conflict cannot be auto-resolved
- **THEN** `userPrompt.promptConflictResolution(path)` is called
- **AND** the user can choose 'upload', 'download', or 'skip'

#### Scenario: IUserPrompt confirms file deletion
- **WHEN** a server-initiated Remove action is encountered and autoConfirm is false
- **THEN** `userPrompt.confirmDeletion(path)` is called
- **AND** the user can confirm or cancel deletion