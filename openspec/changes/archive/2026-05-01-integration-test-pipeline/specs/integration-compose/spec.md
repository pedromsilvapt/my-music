## ADDED Requirements

### Requirement: Integration compose file defines all services
The project SHALL provide a `compose.integration.yaml` that defines all services required to run integration tests: `db` (PostgreSQL), `server`, `client`, `caddy`, and `integration-tests`.

#### Scenario: All services present
- **WHEN** `compose.integration.yaml` is inspected
- **THEN** it contains service definitions for `db`, `server`, `client`, `caddy`, and `integration-tests`
- **AND** no dev-only services (pgAdmin, Dozzle) are included

### Requirement: Image tags use single VERSION environment variable
All custom Docker image tags in `compose.integration.yaml` SHALL use a single `VERSION` environment variable substitution with a sensible default, ensuring all services run the same build.

#### Scenario: Default version used when no env var set
- **WHEN** `docker compose -f compose.integration.yaml up` is run without setting `VERSION`
- **THEN** all service images use the default tag value (e.g., `dev`)

#### Scenario: Custom version via environment variable
- **WHEN** `VERSION=sha-abc123 docker compose -f compose.integration.yaml up` is run
- **THEN** all service images (server, client, integration-tests) resolve to tag `sha-abc123`

### Requirement: IntegrationTests service depends on other services being healthy
The `integration-tests` service SHALL depend on `db` and `caddy` (which proxies to `server` and `client`) with health check conditions, ensuring tests only start when the stack is ready.

#### Scenario: Services must be healthy before tests start
- **WHEN** `docker compose -f compose.integration.yaml up` is executed
- **THEN** the `integration-tests` container does not start until `db` reports healthy
- **AND** the `integration-tests` container does not start until `caddy` reports healthy

### Requirement: IntegrationTests service runs test suite and exits with result code
The `integration-tests` container entrypoint SHALL run `dotnet vstest` with the integration runsettings file and propagate the test exit code as the container exit code.

#### Scenario: All tests pass
- **WHEN** integration tests execute successfully
- **THEN** the container exits with code 0

#### Scenario: Tests fail
- **WHEN** one or more integration tests fail
- **THEN** the container exits with a non-zero code

### Requirement: Caddy configuration for integration environment
The Caddy service in `compose.integration.yaml` SHALL listen on port 5001 and proxy API requests to the `server` service and frontend requests to the `client` service, matching the application's expected base URL.

#### Scenario: API requests proxied to server
- **WHEN** a request is sent to Caddy on port 80 at `/api/*`
- **THEN** the request is proxied to the `server` service

#### Scenario: Frontend requests proxied to client
- **WHEN** a request is sent to Caddy on port 80 at any non-API path
- **THEN** the request is proxied to the `client` service

### Requirement: Clean teardown with volume removal
The `compose.integration.yaml` SHALL define a named volume for PostgreSQL data that can be removed on `docker compose down --volumes`.

#### Scenario: Volumes removed on teardown
- **WHEN** `docker compose -f compose.integration.yaml down --volumes` is executed
- **THEN** the PostgreSQL data volume is removed along with containers