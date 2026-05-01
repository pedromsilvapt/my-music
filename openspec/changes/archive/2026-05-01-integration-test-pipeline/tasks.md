## 1. Runsettings & Base URL Configuration

- [x] 1.1 Update `MyMusic.IntegrationTests/.runsettings` to add `BASE_URL` environment variable (empty default)
- [x] 1.2 Verify `MyMusic.IntegrationTests/Base/IntegrationTestBase.cs` already reads `BASE_URL` from env with fallback to `http://localhost:5001`
- [x] 1.3 Update `MyMusic.IntegrationTests/integration.runsettings` to set `BASE_URL=http://caddy`

## 2. Compose Integration File Alignment

- [x] 2.1 Verify `compose.integration.yaml` Caddy config listens on port `:80` and proxies `/api/*` to `server:8080` and `/*` to `client:8080`
- [x] 2.2 Verify all services use the `VERSION` env var pattern consistently (`${VERSION:-dev}`)
- [x] 2.3 Ensure the `integration-tests` service passes `BASE_URL=http://caddy` in its environment

## 3. Earthfile Integration Tests Target

- [x] 3.1 Verify root `Earthfile` `docker-integration-tests` target matches compose image references and includes Playwright, CLI `.deb`, and `integration.runsettings`
- [x] 3.2 Verify the entrypoint uses `dotnet vstest MyMusic.IntegrationTests.dll --settings integration.runsettings`

## 4. GitHub Actions Integration Tests Workflow

- [x] 4.1 Create `.github/workflows/integration-tests.yml` with a workflow triggered on push/PR to main and manual dispatch
- [x] 4.2 Add step to set up Earthly CLI in the workflow
- [x] 4.3 Add steps to build Server, Client, and IntegrationTests Docker images via Earthly with matching `VERSION` tag
- [x] 4.4 Add step to run `docker compose -f compose.integration.yaml up --exit-code-from integration-tests` with the `VERSION` tag
- [x] 4.5 Add a post-run step (always runs) to execute `docker compose -f compose.integration.yaml down --volumes` for cleanup

## 5. Verification

- [ ] 5.1 Build the IntegrationTests Docker image using the Earthly target and verify CLI binary exists at `/usr/local/bin/my-music`
- [ ] 5.2 Run `docker compose -f compose.integration.yaml up` and verify all services start and tests execute
- [x] 5.3 Verify the existing local test workflow (`dotnet test` in `MyMusic.IntegrationTests`) still works with the updated `.runsettings`