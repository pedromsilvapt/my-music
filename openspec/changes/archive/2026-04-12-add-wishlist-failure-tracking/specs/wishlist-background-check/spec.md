## MODIFIED Requirements

### Requirement: Background service handles source API errors gracefully
The background service SHALL handle errors without crashing.

#### Scenario: Source API error logs warning and updates failure tracking
- **WHEN** source API call fails for a wishlist item
- **THEN** service logs warning, increments ContinuousFailedCount, sets LastErrorMessage, skips that item, continues processing others

#### Scenario: Source not found logs warning and updates failure tracking
- **WHEN** source referenced by wishlist item no longer exists
- **THEN** service logs warning, increments ContinuousFailedCount, sets LastErrorMessage, skips that item

#### Scenario: Successful check resets failure tracking
- **WHEN** background service successfully processes a wishlist item
- **THEN** ContinuousFailedCount is reset to 0 and LastErrorMessage is cleared
