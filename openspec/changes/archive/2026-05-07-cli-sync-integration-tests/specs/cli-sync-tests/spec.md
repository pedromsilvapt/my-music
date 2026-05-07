## MODIFIED Requirements

### Requirement: Test Conciseness
Each test step SHALL be implemented in one or two lines of code. Tests in partial class files SHALL share the same fixture initialization pattern as the main `CliSyncTests` class.

#### Scenario: Setup file step
- **WHEN** test needs to create a test file
- **THEN** a single method call creates the file in the repository

#### Scenario: Sync step
- **WHEN** test needs to run sync
- **THEN** a single method call executes sync and returns result

#### Scenario: Verify step
- **WHEN** test needs to verify file metadata
- **THEN** a single assertion call validates the metadata