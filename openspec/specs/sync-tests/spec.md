## ADDED Requirements

### Requirement: xUnit test infrastructure for CLI sync services
The system SHALL set up xUnit tests for CLI sync services in `MyMusic.CLI.Tests/Services/Sync/` with NSubstitute mocks and Shouldly assertions.

#### Scenario: xUnit can run CLI sync tests
- **WHEN** `dotnet test` is run
- **THEN** xUnit discovers and runs tests in `MyMusic.CLI.Tests/Services/Sync/`
- **AND** NSubstitute creates mocks for all interfaces
- **AND** Shouldly provides clear assertion messages

### Requirement: Unit tests for CLI atomic upload operation
The system SHALL provide unit tests for `UploadOneFileAsync` covering: successful upload, failed upload (API error), and dry-run behavior.

#### Scenario: Test UploadOneFileAsync success
- **WHEN** ISyncApiClient.UploadFileAsync resolves successfully
- **THEN** the returned record item has action 'Created' and source 'Device'

#### Scenario: Test UploadOneFileAsync failure
- **WHEN** ISyncApiClient.UploadFileAsync throws an exception
- **THEN** the returned record item has action 'Error' with the exception message

### Requirement: Unit tests for CLI atomic download operation
The system SHALL provide unit tests for `DownloadOneFileAsync` covering: successful download with directory creation, download with existing file replacement, failed download.

#### Scenario: Test DownloadOneFileAsync replaces existing file
- **WHEN** IFileOps.FileExists returns true at target path
- **THEN** IFileOps.DeleteFileAsync is called before write

#### Scenario: Test DownloadOneFileAsync creates parent directory
- **WHEN** parent directory does not exist
- **THEN** IFileOps.EnsureDirectory is called

### Requirement: Unit tests for CLI atomic remove operation
The system SHALL provide unit tests for `RemoveOneFileAsync` covering: successful removal, missing file (still acknowledges), user cancellation via IUserPrompt.

#### Scenario: Test RemoveOneFileAsync with user confirmation
- **WHEN** IUserPrompt.ConfirmDeletionAsync resolves true
- **THEN** IFileOps.DeleteFileAsync is called
- **AND** ISyncApiClient.AcknowledgeActionAsync is called

#### Scenario: Test RemoveOneFileAsync with user cancellation
- **WHEN** IUserPrompt.ConfirmDeletionAsync resolves false
- **THEN** IFileOps.DeleteFileAsync is NOT called
- **AND** ISyncApiClient.AcknowledgeActionAsync is NOT called

### Requirement: Unit tests for CLI orchestrator with SyncDirection
The system SHALL provide unit tests for the orchestrator covering: SyncDirection.Up skips server actions, SyncDirection.Down skips upload, SyncDirection.Both executes all phases.

#### Scenario: Test SyncDirection.Up skips server actions
- **WHEN** orchestrator is called with SyncDirection.Up
- **THEN** uploadPhase is called
- **AND** serverActionsPhase is NOT called

#### Scenario: Test SyncDirection.Down skips upload
- **WHEN** orchestrator is called with SyncDirection.Down
- **THEN** uploadPhase is NOT called
- **AND** serverActionsPhase is called

### Requirement: Unit tests for CLI progress reporting
The system SHALL provide unit tests verifying IProgress<SyncProgress> is called during sync execution.

#### Scenario: Progress is reported during upload
- **WHEN** uploadPhase processes files
- **THEN** IProgress.Report is called with current phase and counts

### Requirement: Jest test infrastructure for mobile sync services
The system SHALL set up Jest as the test runner for `MyMusic.Mobile` unit tests, configured to work with TypeScript and the project's module structure.

#### Scenario: Jest can run sync service tests
- **WHEN** `npm test` is run in the MyMusic.Mobile directory
- **THEN** Jest discovers and runs tests in `src/services/sync/__tests__/`
- **AND** TypeScript is transpiled correctly for test execution

### Requirement: Unit tests for atomic upload operation
The system SHALL provide unit tests for `uploadOneFile` covering: successful upload, failed upload (API error), and dry-run behavior.

#### Scenario: Test uploadOneFile success
- **WHEN** apiClient.uploadFile resolves successfully
- **THEN** the returned record item has action 'Created' and source 'Device'

#### Scenario: Test uploadOneFile failure
- **WHEN** apiClient.uploadFile rejects with an error
- **THEN** the returned record item has action 'Error' with the error message

#### Scenario: Test uploadOneFile dry-run
- **WHEN** options.dryRun is true
- **THEN** no API call is made and the result is recorded as 'Created'

### Requirement: Unit tests for atomic download operation
The system SHALL provide unit tests for `downloadOneFile` covering: successful download with directory creation, download with existing file replacement, failed download, and dry-run behavior.

#### Scenario: Test downloadOneFile replaces existing file
- **WHEN** a file exists at the target path
- **THEN** the existing file is deleted before writing the new content

#### Scenario: Test downloadOneFile creates parent directory
- **WHEN** the parent directory does not exist
- **THEN** the directory is created before writing

#### Scenario: Test downloadOneFile acknowledges after write
- **WHEN** the file is successfully written
- **THEN** acknowledgeAction is called with the correct devicePath and modification time

### Requirement: Unit tests for atomic remove operation
The system SHALL provide unit tests for `removeOneFile` covering: successful removal, missing file (still acknowledges), user cancellation, and autoConfirm behavior.

#### Scenario: Test removeOneFile with user confirmation
- **WHEN** userPrompt.confirmDeletion resolves true
- **THEN** the file is deleted and acknowledged

#### Scenario: Test removeOneFile with user cancellation
- **WHEN** userPrompt.confirmDeletion resolves false
- **THEN** the file is NOT deleted and NOT acknowledged

#### Scenario: Test removeOneFile autoConfirm skips prompt
- **WHEN** options.autoConfirm is true
- **THEN** userPrompt.confirmDeletion is NOT called
- **AND** the file is deleted and acknowledged

#### Scenario: Test removeOneFile missing file still acknowledges
- **WHEN** fileOps.fileExists returns false
- **THEN** acknowledgeAction is still called
- **AND** no removal record is created

### Requirement: Unit tests for resolve conflicts phase
The system SHALL provide unit tests for `resolveConflictsPhase` covering: auto-resolved conflicts become toUpdate, unresolved conflicts prompt user, treatConflictsAsErrors auto-skips, and dry-run behavior.

#### Scenario: Test auto-resolved conflicts
- **WHEN** the server returns resolved items
- **THEN** their paths are added to the toUpdate set without user prompts

#### Scenario: Test treatConflictsAsErrors
- **WHEN** treatConflictsAsErrors is true and unresolved conflicts exist
- **THEN** result.failed is incremented for each conflict
- **AND** userPrompt.promptConflictResolution is NOT called

### Requirement: Unit tests for complete phase
The system SHALL provide unit tests for `completePhase` covering: recording download/remove results, completing the session, saving metadata, and receiving authoritative server counts.

#### Scenario: Test authoritative server counts override client estimates
- **WHEN** completePhase receives a completeSync response
- **THEN** result counters are overwritten with server values

#### Scenario: Test lastSyncAt and lastScanTotal are saved
- **WHEN** completePhase finishes successfully
- **THEN** config.setLastSyncAt is called
- **AND** config.setLastScanTotal is called with the scanned file count