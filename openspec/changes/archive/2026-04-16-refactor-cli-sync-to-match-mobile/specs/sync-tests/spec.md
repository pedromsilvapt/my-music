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
