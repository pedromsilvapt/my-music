## ADDED Requirements

### Requirement: GitHub Actions workflow builds all Docker images
The project SHALL include a GitHub Actions workflow (`.github/workflows/integration-tests.yml`) that builds all Docker images needed for the integration test pipeline using Earthly.

#### Scenario: All images built in CI
- **WHEN** the workflow runs
- **THEN** the Server Docker image is built using the Earthly `+docker` target
- **AND** the Client Docker image is built using the Client Earthfile `+docker` target
- **AND** the IntegrationTests Docker image is built using the Earthly `+docker-integration-tests` target
- **AND** no images are pushed to a registry

### Requirement: Workflow runs integration tests via Docker Compose
The workflow SHALL start the integration test stack using `docker compose -f compose.integration.yaml up --exit-code-from integration-tests`.

#### Scenario: Workflow starts compose stack and runs tests
- **WHEN** the workflow executes
- **THEN** `docker compose -f compose.integration.yaml up --exit-code-from integration-tests` is run
- **AND** the workflow exit code reflects the integration test results

### Requirement: Workflow always tears down with volume removal
The workflow SHALL always run `docker compose -f compose.integration.yaml down --volumes` after the test run, regardless of success or failure.

#### Scenario: Successful test run cleanup
- **WHEN** integration tests pass
- **THEN** `docker compose -f compose.integration.yaml down --volumes` is still executed to remove containers and volumes

#### Scenario: Failed test run cleanup
- **WHEN** integration tests fail
- **THEN** `docker compose -f compose.integration.yaml down --volumes` is still executed to remove containers and volumes
- **AND** the workflow still reports a failure

### Requirement: Workflow uses consistent image version
The workflow SHALL set a `VERSION` environment variable (e.g., based on commit SHA) that is passed consistently to all Docker image builds and the compose run.

#### Scenario: All images use same version tag
- **WHEN** the workflow builds images and starts compose
- **THEN** all services (server, client, integration-tests) reference images with the same version tag