## Context

Integration tests currently require a developer to manually start PostgreSQL, the Server, the Client, and Caddy, then run `dotnet test` locally. The CLI binary is discovered by walking up from the test assembly's directory to find `MyMusic.sln` and constructing a path to `MyMusic.CLI/bin/Debug/net10.0/my-music`. This approach is fragile, environment-dependent, and cannot run in CI without significant manual setup.

The existing build system uses Earthly (Earthfile targets for Server, Client, and CLI Docker images) and Docker Compose for local development. The CLI Earthfile already produces a `.deb` package but the integration tests don't consume it.

The existing `compose.integration.yaml` and `Earthfile` docker-integration-tests target have been partially implemented but need alignment. The compose file's Caddy configuration listens on port 80 (the default HTTP port inside Docker network), while `IntegrationTestBase` defaults to `http://localhost:5001` for local development. The `integration.runsettings` file doesn't set `BASE_URL`. There is no CI workflow.

## Goals / Non-Goals

**Goals:**
- Provide a single `docker compose up` command that spins up all services and runs integration tests end-to-end
- Make image tags configurable via a single `VERSION` environment variable for consistency across all images in the compose file
- Bundle the CLI `.deb` inside the IntegrationTests container so `CLI_PATH` points to `/usr/local/bin/my-music`
- Produce a dedicated `integration.runsettings` that configures correct URLs and paths for the containerized environment, including `BASE_URL`
- Add a `BASE_URL` environment variable to the default `.runsettings` (defaulting to `http://localhost:5001`) so `IntegrationTestBase` can use it uniformly
- Add an Earthfile target that builds the IntegrationTests container image (publish, Playwright browsers, `.deb` installation)
- Create a GitHub Actions workflow that builds all Docker images, runs the compose project with `--exit-code-from integration-tests`, and always tears down with `--volumes`

**Non-Goals:**
- Replacing the existing development `compose.yaml` or local test workflow
- Changing how IntegrationTestBase discovers the server URL at runtime beyond adding env var support
- Publishing Docker images to a registry from CI (only build, not publish)

## Decisions

### Decision 1: Separate compose file for integration tests

**Choice**: New `compose.integration.yaml` alongside existing `compose.yaml`.

**Rationale**: The development compose file has commented-out services, pgAdmin, Dozzle, and other dev-only tooling. A dedicated integration compose file keeps concerns separated and avoids polluting the dev workflow with test-specific configuration.

**Alternatives considered**:
- Extending existing `compose.yaml` with profiles — adds complexity to the dev file and mixes concerns.
- A single compose file with `--profile integration` — similar concern; profiles don't isolate network/volume config as cleanly.

### Decision 2: IntegrationTests container runs tests as entrypoint

**Choice**: The IntegrationTests container's entrypoint runs `dotnet vstest` with `--settings integration.runsettings`, then exits with the test exit code.

**Rationale**: This makes `docker compose up --exit-code-from integration-tests` a natural way to run the suite. The container exit code propagates to CI. Other services (DB, Server, Client, Caddy) must be healthy before tests start—handled via `depends_on` with health checks.

**Alternatives considered**:
- Running tests from outside the compose (e.g., `docker compose run`) — requires the runner to have .NET SDK installed, defeating the purpose of full containerization.

### Decision 3: CLI `.deb` installed inside IntegrationTests container

**Choice**: Build the CLI `.deb` via the existing Earthfile target, copy it into the IntegrationTests image, and install it with `dpkg -i`. Set `CLI_PATH=/usr/local/bin/my-music`.

**Rationale**: The `.deb` already exists as an Earthly artifact. Installing it ensures the integration tests exercise the same packaging that end users receive. The symlink at `/usr/local/bin/my-music` provides a stable, predictable path for `CLI_PATH`.

**Alternatives considered**:
- Copying the raw published binary — doesn't validate the `.deb` packaging works.
- Building CLI inside the test container — couples test image build to CLI build toolchain unnecessarily.

### Decision 4: Single VERSION environment variable for image consistency

**Choice**: All custom image tags in `compose.integration.yaml` use a single `VERSION` environment variable with a default (e.g., `${VERSION:-dev}`).

**Rationale**: Using a single version variable ensures all services run the same build. CI can set `VERSION=sha-abc123` to test a specific commit. Defaults allow running locally without setting any env vars.

**Alternatives considered**:
- Per-service image tag variables — allows version skew between services, which is harder to debug and typically not needed.

### Decision 5: `integration.runsettings` with BASE_URL for container environment

**Choice**: New file `MyMusic.IntegrationTests/integration.runsettings` that sets `CLI_PATH=/usr/local/bin/my-music`, `BROWSER=chromium`, and `BASE_URL=http://caddy` (the Docker Compose internal network address on port 80). The default `.runsettings` also gains a `BASE_URL` env var (empty, defaulting to `http://localhost:5001` in `IntegrationTestBase`).

**Rationale**: The existing `.runsettings` has empty env vars (intended for local override). The integration variant pre-fills them for the containerized environment. Caddy listens on port 80 inside the Docker network, so `http://caddy` resolves correctly. `IntegrationTestBase` already reads `BASE_URL` from env with fallback to `http://localhost:5001`, so adding it to runsettings is a natural extension.

### Decision 6: Caddy listens on port 80 in integration compose

**Choice**: The Caddy configuration in `compose.integration.yaml` listens on port 80 inside the Docker network, matching the existing compose file. The `integration-tests` service accesses Caddy at `http://caddy` (port 80 default).

**Rationale**: Port 80 is the standard HTTP port and avoids conflicting with port 5001 used in local development. Inside the Docker network, services communicate by service name, so `http://caddy` resolves correctly. The port 5001 convention is only for local dev where Caddy proxies on the host.

### Decision 7: GitHub Actions workflow builds images and runs compose

**Choice**: New `.github/workflows/integration-tests.yml` that uses Earthly to build all Docker images (Server, Client, CLI package, IntegrationTests), then runs `docker compose -f compose.integration.yaml up --exit-code-from integration-tests`, and always runs `docker compose -f compose.integration.yaml down --volumes` afterward.

**Rationale**: A single workflow that validates the entire system end-to-end: build all images, spin up the stack, run tests, and clean up. The `--exit-code-from` flag ensures the workflow fails if tests fail. The `down --volumes` ensures no stale data persists between runs. Images are built locally (no push to registry) since this is for validation, not deployment.

**Alternatives considered**:
- Push images to a registry and pull them in a separate workflow — adds latency and requires registry credentials for a validation-only workflow.
- Running tests without Docker Compose (just `dotnet test`) — doesn't validate the containerized deployment.

## Risks / Trade-offs

- **[Test startup ordering]** Tests may start before Server/DB are fully ready → Mitigation: `depends_on` with health checks; compose `condition: service_healthy`.
- **[Container image size]** IntegrationTests image includes .NET SDK + Playwright browsers + CLI → Mitigation: this is a test-only image, not distributed; can optimize with multi-stage builds.
- **[Port convention mismatch]** Local dev uses `http://localhost:5001` via Caddy, but compose network uses `http://caddy` on port 80 → Mitigation: `BASE_URL` env var bridges this gap—the compose environment sets it to `http://caddy` while local dev defaults to `http://localhost:5001`.
- **[Compose file maintenance]** Two compose files to keep in sync → Mitigation: The integration compose is intentionally minimal and doesn't share dev-only services (pgAdmin, Dozzle).
- **[CI runner resources]** Running full Docker Compose stack on GitHub Actions requires significant resources → Mitigation: use `ubuntu-latest` runners which have sufficient resources; consider self-hosted runners if needed.