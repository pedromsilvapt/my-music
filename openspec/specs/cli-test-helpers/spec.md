## ADDED Requirements

### Requirement: CLI Test Fixture Management
The test fixture SHALL create an isolated test environment with a temporary repository directory and CLI configuration file.

#### Scenario: Fixture creates temp directory
- **WHEN** fixture is initialized
- **THEN** a unique temporary directory is created for the repository

#### Scenario: Fixture writes config file
- **WHEN** fixture is initialized with API context and user credentials
- **THEN** a CLI config file is written with server URL and user authentication

#### Scenario: Fixture cleanup on dispose
- **WHEN** fixture is disposed
- **THEN** the temporary repository directory and config file are removed

### Requirement: CLI Process Execution
The `CliRunner` helper SHALL spawn CLI processes as background tasks and capture their output.

#### Scenario: Successful command execution
- **WHEN** `CliRunner.SyncAsync` is called with a configured fixture
- **THEN** the CLI process is spawned and waits for completion
- **THEN** the exit code and captured output are returned

#### Scenario: Command failure captured
- **WHEN** a CLI command fails
- **THEN** the exit code is non-zero and stderr is captured

### Requirement: File Metadata Validation
The `FileValidator` helper SHALL read and validate MP3 metadata using TagLib.

#### Scenario: Validate file exists
- **WHEN** `AssertFileExistsAsync` is called with a valid path
- **THEN** the method returns without exception

#### Scenario: Validate file does not exist
- **WHEN** `AssertFileExistsAsync` is called with an invalid path
- **THEN** the method throws an assertion exception

#### Scenario: Validate metadata matches
- **WHEN** `AssertMetadataAsync` is called with expected title/album/artists
- **THEN** the method reads the file metadata and compares against expected values
