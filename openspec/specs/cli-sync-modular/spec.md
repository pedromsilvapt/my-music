## ADDED Requirements

### Requirement: CLI Sync modular file structure matches Mobile
The CLI sync service SHALL be organized into a modular structure under `MyMusic.CLI/Services/Sync/` with files matching Mobile's organization: Types.cs, Orchestrator.cs, Phases.cs, AtomicOperations.cs, Context.cs, Defaults.cs, and Utils.cs.

#### Scenario: File structure matches Mobile
- **WHEN** the CLI sync refactoring is complete
- **THEN** all 7 files exist in `MyMusic.CLI/Services/Sync/`
- **AND** each file has a clear single responsibility matching its Mobile counterpart

### Requirement: Types.cs defines CLI sync interfaces and types
The system SHALL define all CLI sync interfaces and types in `Types.cs`: SyncResult, SyncProgress, SyncOptions, SyncDirection, RecordItem, PendingActionItem, SyncFileInfoItem, CheckSyncResponse, ConflictItem.

#### Scenario: Types.cs contains all sync types
- **WHEN** Types.cs is created
- **THEN** all sync-related types are defined in one file
- **AND** shared types are imported from MyMusic.Common

### Requirement: Orchestrator.cs coordinates sync phases
The system SHALL provide an `Orchestrator` class that coordinates all sync phases in order, handling cancellation and errors with cleanup in finally blocks.

#### Scenario: Orchestrator executes phases in order
- **WHEN** `OrchestrateSyncAsync` is called
- **THEN** phases execute: start session → get pending → upload (if direction allows) → server actions (if direction allows) → complete
- **AND** exceptions are caught and logged
- **AND** partial results are returned on cancellation

### Requirement: Phases.cs contains individual phase functions
The system SHALL provide phase functions in `Phases.cs` for: StartSessionAsync, GetPendingActionsAsync, UploadPhaseAsync, ResolveConflictsAsync, ServerActionsPhaseAsync, CompleteAsync.

#### Scenario: Each phase is a separate method
- **WHEN** a phase method is called
- **THEN** it performs its specific responsibility
- **AND** it updates the SyncContext
- **AND** it reports progress via IProgress<SyncProgress>

### Requirement: AtomicOperations.cs contains low-level file operations
The system SHALL provide atomic operations in `AtomicOperations.cs` for: UploadOneFileAsync, DownloadOneFileAsync, RemoveOneFileAsync.

#### Scenario: Atomic operations are reusable
- **WHEN** an atomic operation is called
- **THEN** it performs a single file operation
- **AND** it returns a result or record item
- **AND** it handles errors gracefully

### Requirement: Context.cs creates SyncContext
The system SHALL provide a `Context.cs` that creates and initializes SyncContext with all dependencies and initial state.

#### Scenario: SyncContext is initialized correctly
- **WHEN** CreateSyncContext is called
- **THEN** SyncContext is created with deviceId, repositoryPath, sessionId, options, and empty result counters
- **AND** uploadedPaths and pendingDownloadPaths are empty HashSets

### Requirement: Defaults.cs provides CLI-specific implementations
The system SHALL provide CLI-specific implementations in `Defaults.cs` for: IFileOps (System.IO.Abstractions), IKeepAwake (no-op), IUserPrompt (Console prompts).

#### Scenario: CLI IKeepAwake is no-op
- **WHEN** CLI's IKeepAwake.Activate() is called
- **THEN** no operation is performed
- **AND** no error is thrown

#### Scenario: CLI IUserPrompt uses console input
- **WHEN** CLI's IUserPrompt.PromptConflictResolutionAsync is called
- **THEN** the user is prompted via console
- **AND** the user can type a response

### Requirement: SyncDirection controls upload and download phases
The system SHALL support `SyncDirection` enum with values: Both (default), Up (upload only), Down (download only). The orchestrator SHALL skip phases based on direction.

#### Scenario: SyncDirection.Up skips server actions
- **WHEN** SyncDirection is Up
- **THEN** upload phase executes
- **AND** server actions (downloads) are skipped
- **AND** complete phase still executes

#### Scenario: SyncDirection.Down skips upload
- **WHEN** SyncDirection is Down
- **THEN** upload phase is skipped
- **AND** server actions (downloads) execute
- **AND** complete phase still executes

#### Scenario: SyncDirection.Both executes all phases
- **WHEN** SyncDirection is Both (default)
- **THEN** all phases execute in order
- **AND** behavior matches pre-refactor SyncService

### Requirement: Mobile @TODO comments document CLI differences
The system SHALL add @TODO comments in Mobile files documenting CLI-specific features: SyncDirection in phases.ts, verbose option in syncService.ts, conflict resolution differences in types.ts.

#### Scenario: Mobile phases.ts documents SyncDirection
- **WHEN** a developer reads Mobile's phases.ts
- **THEN** they see an @TODO comment noting CLI has SyncDirection for controlling upload/download phases

#### Scenario: Mobile types.ts documents conflict resolution difference
- **WHEN** a developer reads Mobile's types.ts
- **THEN** they see an @TODO comment noting Mobile has treatConflictsAsErrors but CLI uses checksum-based conflict resolution
