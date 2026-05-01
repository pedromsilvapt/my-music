## ADDED Requirements

### Requirement: Earthfile target builds IntegrationTests Docker image
The root Earthfile SHALL include a target that builds a Docker image for running integration tests. The image SHALL contain the published test project, Playwright browsers, and the CLI `.deb` package installed.

#### Scenario: Build integration tests image
- **WHEN** the Earthly `integration-tests` target is executed
- **THEN** a Docker image is produced containing the .NET runtime, Playwright Chromium browser, the published test assembly, and the CLI binary at `/usr/local/bin/my-music`

### Requirement: CLI deb package included in test image
The IntegrationTests image build SHALL copy the CLI `.deb` artifact from the `MyMusic.CLI+package` Earthly target and install it.

#### Scenario: CLI binary available at expected path
- **WHEN** the IntegrationTests container starts
- **THEN** the `my-music` binary is available at `/usr/local/bin/my-music`
- **AND** `CLI_PATH` environment variable is set to `/usr/local/bin/my-music`

### Requirement: Playwright browsers installed in test image
The IntegrationTests image SHALL install Playwright Chromium browser dependencies and the browser itself.

#### Scenario: Playwright can launch Chromium
- **WHEN** the test project runs inside the container
- **THEN** Playwright can launch Chromium without additional setup

### Requirement: Integration runsettings referenced in container entrypoint
The container entrypoint SHALL use `dotnet test --settings integration.runsettings` to configure the test run with container-appropriate paths and URLs.

#### Scenario: Correct runsettings file used
- **WHEN** the IntegrationTests container runs
- **THEN** `dotnet test` is invoked with `--settings integration.runsettings`