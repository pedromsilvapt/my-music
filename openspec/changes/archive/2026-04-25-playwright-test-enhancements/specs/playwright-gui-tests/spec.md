## ADDED Requirements

### Requirement: IntegrationTestBase creates and deletes test users automatically

The IntegrationTestBase SHALL create a test user via API during initialization and delete it during disposal, ensuring all inheriting tests have an isolated user context.

#### Scenario: Test user created on initialization
- **WHEN** a test class inheriting from IntegrationTestBase initializes
- **THEN** a unique test user is created via POST /users API
- **AND** the UserId and UserName are available as protected properties
- **AND** the X-MyMusic-UserName header is set for all API requests

#### Scenario: Test user deleted on disposal
- **WHEN** a test class inheriting from IntegrationTestBase disposes
- **THEN** the test user is deleted via DELETE /users/{id} API
- **AND** all associated data is cleaned up

### Requirement: Playwright tests verify GUI elements

Integration tests using Playwright SHALL verify end-user visible functionality through browser interactions, not implementation details.

#### Scenario: Username displayed in topbar
- **WHEN** a test user navigates to the application
- **THEN** the test user's username is visible in the topbar

#### Scenario: Test focuses on user workflow
- **WHEN** writing an integration test
- **THEN** the test verifies user-visible behavior (clicks, navigation, displayed content)
- **AND** does not test internal service methods or database state directly

### Requirement: Test type separation

The project SHALL maintain clear separation between unit tests and integration tests.

#### Scenario: Business logic tested in unit tests
- **WHEN** testing service behavior, algorithms, or internal methods
- **THEN** the test is written in MyMusic.Common.Tests
- **AND** uses mocked dependencies and in-memory SQLite

#### Scenario: End-user functionality tested in integration tests
- **WHEN** testing user-visible page behavior or workflows
- **THEN** the test is written in MyMusic.IntegrationTests
- **AND** uses Playwright browser interactions

### Requirement: Test running documentation

The project documentation SHALL include instructions for running all tests and specific tests.

#### Scenario: Run all tests
- **WHEN** developer runs `dotnet test` from repository root
- **THEN** all test projects execute (unit tests and integration tests)

#### Scenario: Run specific test project
- **WHEN** developer runs `dotnet test MyMusic.IntegrationTests`
- **THEN** only integration tests execute

#### Scenario: Run specific test by name
- **WHEN** developer runs `dotnet test --filter "FullyQualifiedName~TestName"`
- **THEN** only matching tests execute
