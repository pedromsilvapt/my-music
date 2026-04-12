## ADDED Requirements

### Requirement: Wishlist item stores optional filter expression
The system SHALL allow wishlist items to store an optional Filter expression using the existing Filter DSL format.

#### Scenario: Creating wishlist item with filter
- **WHEN** user creates a wishlist item with query "Michael Jackson" and filter "genre:Pop"
- **THEN** the wishlist item is stored with Query="Michael Jackson" and Filter="genre:Pop"
- **AND** the item's hash is computed from search results matching both the query AND the filter

#### Scenario: Creating wishlist item without filter
- **WHEN** user creates a wishlist item with query "Michael Jackson" and no filter
- **THEN** the wishlist item is stored with Query="Michael Jackson" and Filter=NULL
- **AND** the item behaves identically to pre-change behavior

### Requirement: Filter is part of unique constraint
The system SHALL include the Filter column in the unique index on WishlistItem, treating NULL as a distinct value.

#### Scenario: Same query with different filters are distinct items
- **GIVEN** user has wishlist item with SourceId=1, Query="test", Filter=NULL
- **WHEN** user creates another item with SourceId=1, Query="test", Filter="year:2023"
- **THEN** both items are stored as separate wishlist items

#### Scenario: Duplicate detection includes filter
- **GIVEN** user has wishlist item with SourceId=1, Query="test", Filter="year:2023"
- **WHEN** user attempts to create item with SourceId=1, Query="test", Filter="year:2023"
- **THEN** the existing item is returned (no duplicate created)

### Requirement: Background service respects wishlist filter
The system SHALL apply the wishlist item's stored Filter expression when checking for updates in the background.

#### Scenario: Background check applies stored filter
- **GIVEN** a wishlist item exists with Query="Holly Humberstone", Filter="duration:>180"
- **WHEN** the background service checks for updates
- **THEN** the hash is computed from search results that match both the query AND the duration filter
- **AND** only if these filtered results change is the item marked as Updated

### Requirement: Keep action uses current filter
The system SHALL use the wishlist item's stored Filter when updating hash via the Keep action.

#### Scenario: Keep action respects filter
- **GIVEN** a wishlist item exists with Query="test", Filter="genre:Rock"
- **WHEN** user clicks Keep to acknowledge update
- **THEN** the new hash is computed from current search results matching query "test" AND filter "genre:Rock"
