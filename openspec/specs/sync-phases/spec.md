## ADDED Requirements

### Requirement: Upload phase respects SyncDirection
The upload phase SHALL only execute when SyncDirection is Both or Up. When SyncDirection is Down, the phase returns immediately without processing.

#### Scenario: Upload phase skipped for Down direction
- **WHEN** SyncDirection is Down
- **THEN** uploadPhase returns without processing files
- **AND** no chunks are uploaded

### Requirement: Server actions phase respects SyncDirection
The server actions phase SHALL only process downloads when SyncDirection is Both or Down. When SyncDirection is Up, the phase returns immediately.

#### Scenario: Server actions skipped for Up direction
- **WHEN** SyncDirection is Up
- **THEN** serverActionsPhase returns without processing pending actions
- **AND** no downloads or removals occur

### Requirement: CLI uses checksum-based conflict resolution
The CLI phases SHALL use checksum-based conflict resolution (same as pre-refactor behavior), not the treatConflictsAsErrors flag used by Mobile.

#### Scenario: CLI conflict resolution via checksum
- **WHEN** a conflict is detected during upload
- **THEN** the server compares checksums to determine resolution
- **AND** matching checksums auto-resolve to 'update'
- **AND** mismatched checksums require user prompt (unless autoConfirm)

### Requirement: Scan phase discovers local music files
The system SHALL provide a `scanPhase` function that reads config, scans the local filesystem using the configured scanner, reports progress, records scan errors, and returns the discovered files and estimated total.

#### Scenario: Successful scan reports progress and returns files
- **WHEN** `scanPhase` is called with a configured repository path and scanner
- **THEN** it calls the scanner with the repository path, extensions, and exclude patterns
- **AND** it reports progress via the onProgress callback with scanned count and current directory
- **AND** it returns the ScanResult (files and errors)
- **AND** it updates the estimated total for progress tracking

#### Scenario: Scan errors are recorded to the session
- **WHEN** the scanner returns errors
- **THEN** scanPhase records each error as an 'Error' action record with source 'Device'

#### Scenario: Scan checks for cancellation
- **WHEN** the sync is cancelled during scanning
- **THEN** scanPhase throws SyncCancelledError

### Requirement: Start session phase creates a server sync session
The system SHALL provide a `startSessionPhase` function that calls the API to start a sync session and returns the session ID.

#### Scenario: Start session returns session ID
- **WHEN** `startSessionPhase` is called with a deviceId and options
- **THEN** it calls `apiClient.startSync` with the deviceId, dryRun, and repositoryPath
- **AND** it returns the sessionId from the response

### Requirement: Upload phase processes chunks of files
The system SHALL provide an `uploadPhase` function that processes all local files in chunks, calling checkSync per chunk, resolving conflicts, uploading new/modified files, recording results, and tracking processed paths.

#### Scenario: Upload phase processes each chunk through check-resolve-upload-record
- **WHEN** `uploadPhase` is called with files and a chunk size
- **THEN** it splits files into chunks of chunkSize
- **AND** for each chunk: calls checkSync, resolves conflicts if any, uploads toCreate and toUpdate files, records skipped files, and sends records to server

#### Scenario: Skipped files are recorded
- **WHEN** a file in a chunk is not in toCreate, toUpdate, or pendingDownloadPaths
- **THEN** the file is recorded as 'Skipped' with reason 'Unchanged' and source 'Device'

### Requirement: Resolve conflicts phase handles conflict detection and resolution
The system SHALL provide a `resolveConflictsPhase` function that takes potential conflicts from a checkSync response, sends them to the server for checksum-based resolution, and handles remaining conflicts via user prompts.

#### Scenario: Auto-resolved conflicts become toUpdate
- **WHEN** the server resolves conflicts (matching checksums)
- **THEN** the resolved files are added to the toUpdate set
- **AND** no user prompt is shown

#### Scenario: Unresolved conflicts prompt user for resolution
- **WHEN** the server returns unresolved conflicts and treatConflictsAsErrors is false
- **THEN** the user is prompted with conflictResolution options (upload/download/skip)
- **AND** 'upload' adds the path to toUpdate
- **AND** 'download' calls handleDownloadConflict
- **AND** 'skip' increments the failed counter

#### Scenario: treatConflictsAsErrors auto-skips conflicts
- **WHEN** the server returns unresolved conflicts and treatConflictsAsErrors is true
- **THEN** no user prompt is shown
- **AND** each conflict increments the failed counter

#### Scenario: Dry-run counts conflicts without resolving
- **WHEN** options.dryRun is true and conflicts exist
- **THEN** conflicts are counted but not resolved
- **AND** no resolveConflicts API call is made
- **AND** no user prompt is shown

### Requirement: Server actions phase processes pending downloads and removals
The system SHALL provide a `serverActionsPhase` function that processes server-initiated pending actions (Download and Remove) after all client uploads complete.

#### Scenario: Download actions download and acknowledge
- **WHEN** a pending action has action 'Download'
- **THEN** the file is downloaded from the server, written to the local filesystem, and acknowledged
- **AND** existing files at the path are replaced (always re-download per behavioral rule 14)
- **AND** parent directories are created if they don't exist
- **AND** the result.downloaded counter is incremented
- **AND** a 'Downloaded' record with source 'Server' is created

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

### Requirement: Complete phase finalizes the sync session
The system SHALL provide a `completePhase` function that records download/remove results, completes the sync session on the server, saves lastSyncAt and lastScanTotal, and returns the authoritative server counts.

#### Scenario: Complete phase records and finalizes
- **WHEN** `completePhase` is called
- **THEN** it records any download/remove record items to the server
- **AND** it calls completeSync on the server
- **AND** it overwrites client-side result counters with authoritative server counts
- **AND** it saves lastSyncAt and lastScanTotal via config

### Requirement: Atomic upload operation handles single file upload
The system SHALL provide an `uploadOneFile` function that uploads a single file to the server, returning success with a record item, or failure with an error record item.

#### Scenario: Successful upload
- **WHEN** `uploadOneFile` is called for a file that the server accepts
- **THEN** it calls `apiClient.uploadFile` with the file data, path, and timestamps
- **AND** it returns a record item with action 'Created' or 'Updated' and source 'Device'

#### Scenario: Failed upload
- **WHEN** `uploadOneFile` is called and the upload request fails
- **THEN** it returns a record item with action 'Error', source 'Device', and the error message

### Requirement: Atomic download operation handles single file download
The system SHALL provide a `downloadOneFile` function that downloads a song from the server, writes it to the local filesystem, and acknowledges the action.

#### Scenario: Successful download
- **WHEN** `downloadOneFile` is called with a songId and local path
- **THEN** it ensures the parent directory exists
- **AND** it deletes any existing file at the path (always re-download)
- **AND** it downloads the blob from the server
- **AND** it writes the file bytes
- **AND** it acknowledges the action with the file's modification time
- **AND** it returns a 'Downloaded' record with source 'Server'

#### Scenario: Failed download
- **WHEN** the download or write fails
- **THEN** the function increments result.failed
- **AND** returns an 'Error' record with the error message

### Requirement: Atomic remove operation handles single file deletion
The system SHALL provide a `removeOneFile` function that deletes a local file and acknowledges the action.

#### Scenario: File exists and is deleted
- **WHEN** the local file exists and (autoConfirm or user confirms)
- **THEN** the file is deleted
- **AND** the action is acknowledged
- **AND** a 'Removed' record with source 'Server' is returned

#### Scenario: File does not exist
- **WHEN** the local file does not exist
- **THEN** the action is still acknowledged
- **AND** no 'Removed' record is created
- **AND** the function returns null (no record)

#### Scenario: User cancels deletion
- **WHEN** autoConfirm is false and the user declines deletion
- **THEN** the file is not deleted, not acknowledged
- **AND** null is returned (no record)

### Requirement: Shared scanner types eliminate duplication
The system SHALL extract `FileMetadata`, `ScanError`, `ScanOptions`, `ScanResult` types and shared helper functions (`shouldExclude`, `globToRegex`, `escapeRegExp`, `fromEpochTimestamp`, `yieldToUI`) from scanner files into shared modules.

#### Scenario: Both scanners import from shared types
- **WHEN** the shared scanner types are extracted
- **THEN** `fileScanner.ts` and `mediaLibraryScanner.ts` both import `FileMetadata`, `ScanError`, `ScanOptions`, `ScanResult` from a shared location
- **AND** both import shared helpers from a shared utils location
- **AND** no duplicate type or function definitions remain