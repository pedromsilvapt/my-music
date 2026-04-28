## ADDED Requirements

### Requirement: CLI Sync Upload Test
The integration test SHALL verify that files in the CLI repository are uploaded to the server during sync.

#### Scenario: New file uploaded on first sync
- **WHEN** a test MP3 file is placed in the repository
- **AND** the CLI sync command is executed
- **THEN** the file appears in the web UI songs collection
- **AND** the CLI exits with success code

### Requirement: CLI Sync Download Test
The integration test SHALL verify that server-side edits are downloaded to the CLI repository during sync.

#### Scenario: Server edit reflected in local file
- **WHEN** a song title is edited via the web UI
- **AND** the CLI sync command is executed
- **THEN** the local MP3 file metadata contains the new title

### Requirement: Test Conciseness
Each test step SHALL be implemented in one or two lines of code.

#### Scenario: Setup file step
- **WHEN** test needs to create a test file
- **THEN** a single method call creates the file in the repository

#### Scenario: Sync step
- **WHEN** test needs to run sync
- **THEN** a single method call executes sync and returns result

#### Scenario: Verify step
- **WHEN** test needs to verify file metadata
- **THEN** a single assertion call validates the metadata
