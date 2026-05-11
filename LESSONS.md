# Lessons Learned

Lessons extracted from code review threads, applicable to this project.

## Integration Tests

### Comment Style
Integration tests use intent-focused, expectation-driven, narrative comments that tell the story of the test (what/why), not mechanical "Arrange/Act/Assert" labels (those are for unit tests only).

### Flows That Perform Assertions
Flows that perform assertions should not return values for external assertion. They perform assertions internally. Naming can be `Validate*` or `Should*` depending on what reads best in each case (e.g., `ValidateSongDetailsFlow`, `ShouldSongExistFlow(shouldExist: true)`).

### Tests Wait for UI State, Not Network
Tests should react to what users see (dialogs disappearing, collections reloading), not inspect network internals with `WaitForResponseAsync`. Users don't watch network tabs—they observe page changes.

### Dialog Components Encapsulate Their Lifecycle
Methods like `ConfirmAsync()` and `CancelAsync()` should wait for the dialog to disappear, not leave that to the caller. All logic regarding locators and page DOM structure should live inside Page/Component Models.

### Timeout Configuration
Use Playwright's `SetDefaultTimeout()` globally instead of scattering `Timeout = 5000` across the codebase.

### Extract Reusable Flows
When the same multi-step logic appears (select songs → open menu → click action), extract it into composable flows.

## Page/Component Models

### Navigation Methods Handle Loading Waits
`GoToSongsAsync()`, `GoToAlbumsAsync()`, `GoToArtistsAsync()` already call `WaitForLoadedAsync()` on their collections. Calling it again is redundant.

### Page Objects Use Scoped Root Locators
All page objects pass their `data-testid` to `BasePage` constructor and chain child locators from `Root` property for test isolation.

### CollectionComponent Provides Existence Checks
Use `HasItemByTextAsync()` instead of building Playwright locators in tests/flows.

### Client Pages Require Root Testids
Every route page component in `MyMusic.Client` has a unique `data-testid` on its root element (e.g., `data-testid="albums-page"`).
