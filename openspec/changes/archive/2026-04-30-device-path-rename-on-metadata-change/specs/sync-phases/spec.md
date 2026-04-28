## MODIFIED Requirements

### Requirement: Server actions phase processes pending downloads and removals
The system SHALL provide a `serverActionsPhase` function that processes server-initiated pending actions (Download and Remove) after all client uploads complete. Download actions that include a `PreviousPath` SHALL download to the new path, then remove the old file and clean up empty parent directories. This requirement applies to both CLI and Mobile implementations.

#### Scenario: Download actions download and acknowledge
- **WHEN** a pending action has action 'Download'
- **THEN** the file is downloaded from the server, written to the local filesystem at the path specified in `Path`, and acknowledged
- **AND** existing files at the path are replaced (always re-download per behavioral rule 14)
- **AND** parent directories are created if they don't exist
- **AND** the result.downloaded counter is incremented
- **AND** a 'Downloaded' record with source 'Server' is created

#### Scenario: Download with PreviousPath renames the file safely (CLI)
- **WHEN** a pending Download action has a non-null PreviousPath in CLI
- **THEN** the file is downloaded to the new path (Path) first
- **AND** if a file exists at the old path (PreviousPath), it is deleted after the download succeeds
- **AND** empty parent directories left by the old file are cleaned up
- **AND** the acknowledge request includes PreviousDevicePath set to PreviousPath
- **AND** DevicePath in the acknowledge request is set to the new Path

#### Scenario: Download with PreviousPath renames the file safely (Mobile)
- **WHEN** a pending Download action has a non-null previousPath in Mobile
- **THEN** the file is downloaded to the new path (path) first
- **AND** if a file exists at the old path (previousPath), it is deleted after the download succeeds
- **AND** empty parent directories left by the old file are cleaned up
- **AND** the acknowledge request includes previousDevicePath set to previousPath
- **AND** devicePath in the acknowledge request is set to the new path

#### Scenario: Download with PreviousPath where old file does not exist
- **WHEN** a pending Download action has a non-null PreviousPath and no file exists at the old path
- **THEN** the file is downloaded to the new path
- **AND** no deletion of the old file is attempted
- **AND** the acknowledge request still includes PreviousDevicePath

#### Scenario: Remove actions delete local file and acknowledge
- **WHEN** a pending action has action 'Remove'
- **THEN** the local file is deleted (after user confirmation unless autoConfirm)
- **AND** the action is acknowledged
- **AND** a 'Removed' record with source 'Server' is created
- **AND** result.removed is incremented

#### Scenario: Remove action with missing local file still acknowledges
- **WHEN** a pending action has action 'Remove' and the local file does not exist
- **THEN** the action is still acknowledged (file already gone)
- **AND** no removal record is created
- **AND** no error is recorded

#### Scenario: Remove action cancelled by user is skipped
- **WHEN** a pending action has action 'Remove' and the user declines deletion
- **THEN** the file is not deleted and the action is not acknowledged
- **AND** no removal record is created

#### Scenario: Dry-run skips download and remove execution
- **WHEN** options.dryRun is true
- **THEN** no files are downloaded, deleted, or acknowledged
- **AND** results are still incremented for UI progress

#### Scenario: Server actions check for cancellation
- **WHEN** sync is cancelled during server actions processing
- **THEN** SyncCancelledError is thrown

### Requirement: Atomic download operation handles single file download
The system SHALL provide a `downloadOneFile` function that downloads a song from the server, writes it to the local filesystem, and acknowledges the action. When a `previousPath` is provided, the function SHALL download to the new path first, then delete the old file and clean empty directories.

#### Scenario: Successful download
- **WHEN** `downloadOneFile` is called with a songId and local path
- **THEN** it ensures the parent directory exists
- **AND** it deletes any existing file at the path (always re-download)
- **AND** it downloads the blob from the server
- **AND** it writes the file bytes
- **AND** it acknowledges the action with the file's modification time
- **AND** it returns a 'Downloaded' record with source 'Server'

#### Scenario: Successful download with rename (previousPath provided)
- **WHEN** `downloadOneFile` is called with a previousPath and the download succeeds
- **THEN** the new file is written to the new path first
- **AND** if a file exists at previousPath, it is deleted
- **AND** empty parent directories from previousPath are cleaned up
- **AND** the acknowledge request includes PreviousDevicePath set to previousPath
- **AND** it returns a 'Downloaded' record with source 'Server'

#### Scenario: Failed download
- **WHEN** the download or write fails
- **THEN** the function increments result.failed
- **AND** returns an 'Error' record with the error message
