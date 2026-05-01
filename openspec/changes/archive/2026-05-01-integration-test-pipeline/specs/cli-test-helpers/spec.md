## MODIFIED Requirements

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