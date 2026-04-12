## Requirements

### Requirement: Wishlist item tracks continuous failure count
The system SHALL track how many consecutive times a background check has failed for each wishlist item.

#### Scenario: Failure increments counter
- **WHEN** a background check fails for a wishlist item
- **THEN** the item's ContinuousFailedCount is incremented by 1

#### Scenario: Success resets counter
- **WHEN** a background check succeeds for a wishlist item
- **THEN** the item's ContinuousFailedCount is reset to 0

### Requirement: Wishlist item stores last error message
The system SHALL store the most recent error message when a background check fails.

#### Scenario: Failure stores error message
- **WHEN** a background check fails for a wishlist item
- **THEN** the item's LastErrorMessage is set to the exception message (truncated to 1024 chars)

#### Scenario: Success clears error message
- **WHEN** a background check succeeds for a wishlist item
- **THEN** the item's LastErrorMessage is set to NULL

### Requirement: Failure tracking fields are included in API responses
The system SHALL expose ContinuousFailedCount and LastErrorMessage in wishlist API responses.

#### Scenario: List wishlist includes failure tracking
- **WHEN** user requests their wishlist items
- **THEN** each item includes ContinuousFailedCount and LastErrorMessage fields

#### Scenario: Get single wishlist item includes failure tracking
- **WHEN** user requests a single wishlist item
- **THEN** the response includes ContinuousFailedCount and LastErrorMessage fields