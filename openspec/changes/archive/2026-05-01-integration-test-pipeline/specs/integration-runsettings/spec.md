## ADDED Requirements

### Requirement: Integration runsettings file for containerized execution
The project SHALL include a `MyMusic.IntegrationTests/integration.runsettings` file specifically for running tests inside the Docker Compose integration environment.

#### Scenario: Runsettings sets CLI_PATH to deb-installed binary
- **WHEN** the `integration.runsettings` file is used for a test run
- **THEN** the `CLI_PATH` environment variable is set to `/usr/local/bin/my-music`

#### Scenario: Runsettings sets browser configuration
- **WHEN** the `integration.runsettings` file is used for a test run
- **THEN** the `BROWSER` environment variable is set to `chromium`
- **AND** the Playwright browser name is set to `chromium`

#### Scenario: Runsettings sets BASE_URL for container network
- **WHEN** the `integration.runsettings` file is used for a test run
- **THEN** the `BASE_URL` environment variable is set to `http://caddy` (the internal Docker Compose network address on port 80)

### Requirement: Default runsettings supports BASE_URL configuration
The existing `MyMusic.IntegrationTests/.runsettings` file SHALL include a `BASE_URL` environment variable (empty by default) so that `IntegrationTestBase` can read it uniformly across local and containerized runs.

#### Scenario: Local runsettings with empty BASE_URL
- **WHEN** a developer runs `dotnet test` in the IntegrationTests project without specifying a settings file
- **THEN** the default `.runsettings` is used with empty `CLI_PATH`, `BROWSER`, and `BASE_URL` values
- **AND** `IntegrationTestBase` falls back to `http://localhost:5001`

#### Scenario: BASE_URL override via environment
- **WHEN** the `BASE_URL` environment variable is set externally (e.g., in CI or compose environment)
- **THEN** `IntegrationTestBase` uses the provided URL instead of the default