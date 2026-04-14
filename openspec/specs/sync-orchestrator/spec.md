## ADDED Requirements

### Requirement: SyncContext carries shared state through the pipeline
The system SHALL define a `SyncContext` class/object that carries all shared state through the sync pipeline: `deviceId`, `repositoryPath`, `decodedRepoPath`, `sessionId`, `options`, `result` (the accumulating SyncResult counters), `uploadedPaths` (Set), `pendingDownloadPaths` (Set), and `pendingActions`.

#### Scenario: SyncContext is constructed from config
- **WHEN** a sync begins
- **THEN** SyncContext is initialized with deviceId, repositoryPath, decodedRepoPath, and options from ISyncConfig and ISyncState
- **AND** result is initialized with all counters at zero
- **AND** uploadedPaths and pendingDownloadPaths are empty Sets

#### Scenario: Phase functions receive SyncContext
- **WHEN** a phase function is called
- **THEN** it receives the SyncContext as its first argument
- **AND** it can read and mutate the context fields relevant to its phase
- **AND** mutations (e.g., incrementing result counters) are visible to subsequent phases

### Requirement: SyncDeps groups all injectable dependencies
The system SHALL define a `SyncDeps` object type that groups all injectable dependency interfaces: `apiClient` (ISyncApiClient), `config` (ISyncConfig), `state` (ISyncState), `scanner` (IFileSystemScanner), `fileOps` (IFileOps), `keepAwake` (IKeepAwake), `userPrompt` (IUserPrompt).

#### Scenario: SyncDeps is passed to the orchestrator
- **WHEN** the orchestrator is called
- **THEN** it receives SyncDeps containing all required interfaces
- **AND** the orchestrator passes relevant subsets to each phase function

#### Scenario: Production SyncDeps are wired up from real implementations
- **WHEN** the production entry point creates SyncDeps
- **THEN** each interface is bound to the real implementation (api/sync.ts, configService, useSyncStore, scannerRegistry, expo-file-system File, expo-keep-awake, Alert)

### Requirement: Orchestrator composes phases into a complete sync
The system SHALL provide an `orchestrateSync` function that composes all phase functions in order: activate keep-awake → scan → start session → get pending actions → upload chunks (with conflict resolution) → server actions → complete → save metadata. The orchestrator SHALL handle cancellation (SyncCancelledError) and ensure keep-awake is deactivated in a finally block.

#### Scenario: Full sync executes all phases in order
- **WHEN** orchestrateSync is called with valid deps and context
- **THEN** phases execute in order: scan → start → pending-actions → upload → server-actions → complete
- **AND** the final SyncResult with authoritative server counts is returned

#### Scenario: Cancellation returns partial result
- **WHEN** sync is cancelled at any point
- **THEN** SyncCancelledError is caught
- **AND** the partial result with `cancelled: true` is returned
- **AND** keep-awake is deactivated

#### Scenario: Error propagates after cleanup
- **WHEN** an unexpected error occurs during sync
- **THEN** the error is re-thrown after result.failed is incremented
- **AND** keep-awake is deactivated in the finally block

#### Scenario: Keep-awake is always deactivated
- **WHEN** sync ends for any reason (success, error, cancellation)
- **THEN** `keepAwake.deactivate()` is called exactly once in a finally block

### Requirement: runSync export maintains backward compatibility
The system SHALL maintain the existing `runSync(onProgress)` export signature. The implementation SHALL construct production SyncDeps, initialize SyncContext, and delegate to `orchestrateSync`.

#### Scenario: runSync has the same signature and behavior
- **WHEN** existing code calls `runSync(onProgress)`
- **THEN** the result is identical to the pre-refactor runSync
- **AND** no caller code changes are required