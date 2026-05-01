## Why

Integration tests currently require a manually-started local environment (dev server, client, database, Caddy) and a locally-built CLI binary. There is no containerized pipeline to run the full integration test suite end-to-end with isolated, reproducible infrastructure, and no CI workflow to validate the system automatically. This makes CI unreliable and onboarding harder.

## What Changes

- New `compose.integration.yaml` with all services needed for integration tests: PostgreSQL, Caddy, Server, Client, and an IntegrationTests container
- Image tags in the compose file driven by a single `VERSION` environment variable for consistency across dev/CI environments
- `integration.runsettings` file with `CLI_PATH`, `BROWSER`, and `BASE_URL` set for the containerized environment
- Default `.runsettings` supports a configurable `BASE_URL` environment variable (defaults to `http://localhost:5001`) so both local and containerized runs use the same configuration mechanism
- New Earthfile target to build the IntegrationTests Docker image (publish test project, install Playwright browsers, bundle the CLI `.deb` package)
- New GitHub Actions workflow that builds all Docker images via Earthly, starts the compose project with `--exit-code-from integration-tests`, and always runs `docker compose down --volumes` afterward

## Capabilities

### New Capabilities
- `integration-compose`: Docker Compose configuration for integration test infrastructure (DB, Caddy, Server, Client, IntegrationTests container)
- `integration-docker-build`: Earthfile target and Docker image definition for the IntegrationTests container
- `integration-runsettings`: Dedicated runsettings file for containerized integration test execution with correct CLI_PATH, BROWSER, and BASE_URL
- `integration-ci-workflow`: GitHub Actions workflow for automated image builds and integration test execution

### Modified Capabilities
- `cli-test-helpers`: CLI_PATH resolution updated to support the `.deb`-installed binary path (`/usr/local/bin/my-music`) in addition to the existing Debug build discovery

## Impact

- **New files**: `compose.integration.yaml`, `MyMusic.IntegrationTests/integration.runsettings`, `.github/workflows/integration-tests.yml`, Earthfile integration target additions
- **Existing files**: Root Earthfile (new `docker-integration-tests` target), `MyMusic.IntegrationTests/.runsettings` (add `BASE_URL` env var), `MyMusic.IntegrationTests/Base/IntegrationTestBase.cs` (may need adjustment if runsettings env var approach changes)
- **Dependencies**: Earthly build system, Docker Compose v2, GitHub Actions, existing Server/Client/CLI Earthfile targets
- **No breaking changes**: Existing local test workflow (`dotnet test` with `.runsettings`) remains unchanged; the new compose pipeline is additive