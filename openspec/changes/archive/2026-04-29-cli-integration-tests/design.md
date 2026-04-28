## Context

The CLI is a standalone .NET executable that syncs music files between a local repository and the backend server. Integration tests need to:
1. Spawn CLI processes as background tasks
2. Manage isolated test repositories with temporary directories
3. Validate file system changes and metadata updates
4. Interact with the web UI via Playwright

The existing `IntegrationTestBase` provides Playwright browser context and API request context. Tests follow the fixture pattern with separate input/output models.

## Goals / Non-Goals

**Goals:**
- Create reusable helper classes for CLI integration tests
- Each test step should be 1-2 lines maximum
- Support the sample workflow: setup file → sync → verify UI → edit → sync → validate file

**Non-Goals:**
- Unit tests for CLI internals (already exist in `MyMusic.CLI.Tests`)
- Performance or stress testing
- Testing CLI installation or configuration persistence

## Decisions

### Use Process.Start for CLI Execution
Spawn CLI as an external process rather than calling code directly. This tests the real CLI executable with actual environment and config.

**Alternative considered**: Reference CLI assemblies and call `Program.Main()` directly. Rejected because it doesn't test the actual CLI execution environment.

### Static Helpers for CLI Runner and File Validator
`CliRunner` and `FileValidator` follow the existing `TestFiles` pattern as static classes. Methods are extension-like utilities.

**Alternative considered**: Instance-based with dependency injection. Rejected for simplicity and consistency with existing test helpers.

### Fixture Manages Repository Directory
`CliTestFixture` implements `IAsyncDisposable` to create and cleanup a temp directory. Config file is written to `%APPDATA%/my-music/appsettings.json` with environment variable override for isolation.

### CLI Executable Discovery
Tests assume CLI is built at `MyMusic.CLI/bin/Debug/net10.0/my-music`. The test project should depend on CLI project to ensure it's built.

## Risks / Trade-offs

- **Process timing**: CLI sync may take variable time → Use reasonable timeout (30s default) and check exit code
- **File locking**: Windows may lock files during sync → Tests run on Linux CI, cleanup in finally blocks
- **Parallel test isolation**: Multiple tests could conflict → Each fixture gets unique device name via GUID suffix
