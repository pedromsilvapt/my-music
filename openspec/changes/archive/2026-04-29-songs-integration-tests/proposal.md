## Why

Integration tests currently have no Playwright browser tests for the songs collection page. Test IDs are missing from the navbar, and the generic Collection component lacks test IDs for its table cells. This makes it impossible to write reliable browser-based tests that verify the songs page renders correctly with data.

## What Changes

- Add `data-testid` attributes to sidebar navigation items (nav-songs, nav-albums, nav-artists, etc.)
- Add `data-testid` attributes to Collection component table cells in format `collection-cell-{column}-{rowKey}`
- Create Playwright page objects: `NavbarComponent`, `SongsPage`, `CollectionComponent`
- Create a single integration test: navigate from home → click Songs nav → verify collection appears → verify row count > 0

## Capabilities

### New Capabilities

- `songs-page-integration-tests`: Playwright browser tests for the songs collection page

### Modified Capabilities

- `playwright-gui-tests`: Enhanced with navbar and collection component test utilities

## Impact

- **MyMusic.Client**: React components receive test IDs for stable test selectors
- **MyMusic.IntegrationTests**: New page objects and first songs page test

## Non-Goals

- Testing all CRUD operations on songs (future change)
- Testing collection view modes (list, grid) - only table view initially
- Testing collection filtering, sorting, or search functionality
