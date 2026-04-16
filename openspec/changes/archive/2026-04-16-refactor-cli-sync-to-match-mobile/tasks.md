## 1. Shared Interfaces (MyMusic.Common)

- [x] 1.1 Create `MyMusic.Common/Services/Sync/` folder structure
- [x] 1.2 Create `ISyncApiClient.cs` with all API methods (StartSyncAsync, CheckSyncAsync, UploadFileAsync, RecordChunkAsync, CompleteSyncAsync, GetPendingActionsAsync, AcknowledgeActionAsync, ResolveConflictsAsync, DownloadSongAsync)
- [x] 1.3 Create `ISyncConfig.cs` with configuration access methods (GetDeviceIdAsync, GetRepositoryPath, GetMusicExtensions, GetExcludePatterns, GetChunkSize, GetLastScanTotal, SetLastScanTotal, SetLastSyncAtAsync)
- [x] 1.4 Create `ISyncState.cs` with IsCancelled and Options properties
- [x] 1.5 Create `IFileOps.cs` with file operation methods (FileExists, EnsureDirectory, WriteFileAsync, DeleteFileAsync, ReadFileBase64Async, GetModificationTimeAsync)
- [x] 1.6 Create `IUserPrompt.cs` with PromptConflictResolutionAsync and ConfirmDeletionAsync methods
- [x] 1.7 Create `IKeepAwake.cs` with Activate and Deactivate methods
- [x] 1.8 Create `Types.cs` with shared types (SyncResult, RecordItem, SyncOptions, SyncDirection, ConflictResolution, SyncProgress)
- [x] 1.9 Update MyMusic.Common.csproj with necessary usings/imports

## 2. CLI Sync Folder Structure

- [x] 2.1 Create `MyMusic.CLI/Services/Sync/` folder
- [x] 2.2 Create `Types.cs` with CLI-specific types (imports from MyMusic.Common.Types)
- [x] 2.3 Create `IFileSystemScanner.cs` CLI-specific interface with ScanAsync method

## 3. Context and Defaults (DI Setup)

- [x] 3.1 Create `Context.cs` with SyncContext class and CreateSyncContext factory
- [x] 3.2 Create `Defaults.cs` with CLI implementations:
  - [x] 3.2.1 `CliFileOps` implementing IFileOps (delegates to System.IO.Abstractions)
  - [x] 3.2.2 `CliKeepAwake` implementing IKeepAwake (no-op)
  - [x] 3.2.3 `CliUserPrompt` implementing IUserPrompt (Console.ReadLine based)
  - [x] 3.2.4 `CliSyncConfig` implementing ISyncConfig (delegates to IOptions<MyMusicOptions>)
  - [x] 3.2.5 `CliSyncApiClient` implementing ISyncApiClient (delegates to IMyMusicClient Refit)
  - [x] 3.2.6 `CliSyncState` implementing ISyncState (reads from CancellationToken)

## 4. Atomic Operations

- [x] 4.1 Create `AtomicOperations.cs` with:
  - [x] 4.1.1 `UploadOneFileAsync` method (extract from SyncService.cs)
  - [x] 4.1.2 `DownloadOneFileAsync` method (extract from SyncService.cs)
  - [x] 4.1.3 `RemoveOneFileAsync` method (extract from SyncService.cs)

## 5. Phase Functions

- [x] 5.1 Create `Phases.cs` with:
  - [x] 5.1.1 `StartSessionAsync` phase (extract from SyncService.cs)
  - [x] 5.1.2 `GetPendingActionsAsync` phase (extract from SyncService.cs)
  - [x] 5.1.3 `UploadPhaseAsync` phase with SyncDirection support (extract from SyncService.cs)
  - [x] 5.1.4 `ResolveConflictsAsync` helper (extract from SyncService.cs)
  - [x] 5.1.5 `ServerActionsPhaseAsync` phase with SyncDirection support (extract from SyncService.cs)
  - [x] 5.1.6 `CompleteAsync` phase (extract from SyncService.cs)

## 6. Orchestrator

- [x] 6.1 Create `Orchestrator.cs` with `OrchestrateSyncAsync` method
- [x] 6.2 Implement phase coordination with SyncDirection filtering
- [x] 6.3 Implement cancellation handling (CancellationToken checks)
- [x] 6.4 Implement error handling with partial results
- [x] 6.5 Implement progress reporting via IProgress<SyncProgress>

## 7. SyncDirection Feature

- [x] 7.1 Add SyncDirection enum to SyncOptions (Both=0, Up=1, Down=2)
- [x] 7.2 Update CLI command parser to accept --direction argument
- [x] 7.3 Update UploadPhaseAsync to check SyncDirection (skip if Down)
- [x] 7.4 Update ServerActionsPhaseAsync to check SyncDirection (skip if Up)

## 8. Integration and Cleanup

- [x] 8.1 Update SyncCommand.cs to use new modular SyncService
- [x] 8.2 Register all DI services in Program.cs
- [x] 8.3 Mark old SyncService.cs as deprecated or remove
- [x] 8.4 Verify all existing sync scenarios work identically

## 9. Mobile @TODO Comments

- [x] 9.1 Add @TODO comment to `phases.ts` noting CLI has SyncDirection
- [x] 9.2 Add @TODO comment to `syncService.ts` noting CLI has verbose option
- [x] 9.3 Add @TODO comment to `types.ts` noting conflict resolution differences

## 10. Unit Tests

- [x] 10.1 Create `MyMusic.CLI.Tests/Services/Sync/` test folder
- [x] 10.2 Create tests for `AtomicOperations.cs`:
  - [x] 10.2.1 Test UploadOneFileAsync success
  - [x] 10.2.2 Test UploadOneFileAsync failure
  - [x] 10.2.3 Test DownloadOneFileAsync with directory creation
  - [x] 10.2.4 Test DownloadOneFileAsync replaces existing file
  - [x] 10.2.5 Test RemoveOneFileAsync with user confirmation
  - [x] 10.2.6 Test RemoveOneFileAsync with user cancellation
  - [x] 10.2.7 Test RemoveOneFileAsync missing file still acknowledges
- [x] 10.3 Create tests for `Orchestrator.cs`:
  - [x] 10.3.1 Test SyncDirection.Up skips server actions
  - [x] 10.3.2 Test SyncDirection.Down skips upload
  - [x] 10.3.3 Test SyncDirection.Both executes all phases
  - [x] 10.3.4 Test cancellation returns partial result
  - [x] 10.3.5 Test progress reporting
- [x] 10.4 Create tests for `Phases.cs`:
  - [x] 10.4.1 Test UploadPhaseAsync with SyncDirection.Down skips
  - [x] 10.4.2 Test ServerActionsPhaseAsync with SyncDirection.Up skips
