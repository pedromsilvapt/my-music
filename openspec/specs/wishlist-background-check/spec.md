## ADDED Requirements

### Requirement: Background service runs on configurable interval
The system SHALL provide a background service that checks wishlist items for changes.

#### Scenario: Service starts with application
- **WHEN** application starts
- **THEN** wishlist background service initializes with delay of 30 seconds before first check

#### Scenario: Service runs on interval
- **WHEN** the configurable interval (default 60 minutes) has elapsed
- **THEN** service begins checking all Active wishlist items

### Requirement: Background service checks Active wishlist items
The background service SHALL iterate through all Active wishlist items and re-run their searches.

#### Scenario: Only Active items are checked
- **WHEN** background service runs
- **THEN** only wishlist items with status Active are processed

#### Scenario: Each item is re-searched
- **WHEN** processing a wishlist item
- **THEN** service calls source API with stored query and source ID

### Requirement: Background service detects result changes
The background service SHALL compare new search result hash against stored hash.

#### Scenario: Hash match preserves Active status
- **WHEN** new result hash matches stored hash
- **THEN** wishlist item remains Active

#### Scenario: Hash mismatch sets Updated status
- **WHEN** new result hash differs from stored hash
- **THEN** wishlist item status is set to Updated

### Requirement: Background service updates check timestamp
The background service SHALL record when each wishlist item was last checked.

#### Scenario: Timestamp updated after each check
- **WHEN** background service processes a wishlist item
- **THEN** item's UpdatedAt timestamp is set to current time

### Requirement: Background service handles source API errors gracefully
The background service SHALL handle errors without crashing.

#### Scenario: Source API error logs warning
- **WHEN** source API call fails for a wishlist item
- **THEN** service logs warning, skips that item, continues processing others

#### Scenario: Source not found skips item
- **WHEN** source referenced by wishlist item no longer exists
- **THEN** service logs warning, skips that item

### Requirement: Background service respects result limit
The background service SHALL use the configured max results when computing hashes.

#### Scenario: Hash uses top N results
- **WHEN** computing hash for comparison
- **THEN** service takes top N results (where N = WishlistMaxResultsToHash config value)