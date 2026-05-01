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
The `CliRunner` helper SHALL spawn CLI processes as background tasks and capture their output. The `CLI_PATH` SHALL be resolved from the environment variable first, falling back to the Debug build path for local development.

#### Scenario: Successful command execution
- **WHEN** `CliRunner.SyncAsync` is called with a configured fixture
- **THEN** the CLI process is spawned and waits for completion
- **THEN** the exit code and captured output are returned

#### Scenario: Command failure captured
- **WHEN** a CLI command fails
- **THEN** the exit code is non-zero and stderr is captured

#### Scenario: CLI_PATH from environment variable
- **WHEN** the `CLI_PATH` environment variable is set and non-empty
- **THEN** the CliRunner uses that path as the CLI binary location

#### Scenario: CLI_PATH fallback to Debug build
- **WHEN** the `CLI_PATH` environment variable is empty or not set
- **THEN** the CliRunner falls back to walking up from `AppContext.BaseDirectory` to find `MyMusic.sln` and constructing `MyMusic.CLI/bin/Debug/net10.0/my-music`

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
