## ADDED Requirements

### Requirement: User can create wishlist item
The system SHALL allow users to create a wishlist item from a search query with current result snapshot.

#### Scenario: Create wishlist from successful search
- **WHEN** user clicks "Add to wishlist" with active search results
- **THEN** system creates a wishlist item with source ID, query text, and hash of top N result song IDs (N configurable)

#### Scenario: Prevent duplicate wishlist items
- **WHEN** user attempts to create a wishlist item for same source and query that already exists
- **THEN** system returns existing wishlist item instead of creating duplicate

### Requirement: User can view wishlist items
The system SHALL allow users to list their wishlist items.

#### Scenario: List all wishlist items
- **WHEN** user requests their wishlist
- **THEN** system returns all wishlist items for that user sorted by status (Updated first) then by creation date

#### Scenario: Filter wishlist by source
- **WHEN** user requests wishlist with source ID filter
- **THEN** system returns only wishlist items for that source

### Requirement: User can update wishlist item hash
The system SHALL allow users to update a wishlist item's hash to current search results.

#### Scenario: Update hash resets status
- **WHEN** user updates a wishlist item (clicks "Keep")
- **THEN** system re-runs the search, computes new hash, sets status to Active

#### Scenario: Update hash preserves item
- **WHEN** user updates a wishlist item
- **THEN** wishlist item remains in the list with updated hash and status Active

### Requirement: User can delete wishlist item
The system SHALL allow users to remove a wishlist item.

#### Scenario: Delete removes wishlist item
- **WHEN** user deletes a wishlist item
- **THEN** system removes the item from database and it no longer appears in wishlist

### Requirement: Wishlist item contains stable result fingerprint
Each wishlist item SHALL store a hash representing the sorted song IDs of search results.

#### Scenario: Hash computed from sorted source song IDs
- **WHEN** creating or updating a wishlist item
- **THEN** system takes top N source song IDs (configurable, default 50), sorts them, computes SHA256 hash

#### Scenario: Hash comparison detects changes
- **WHEN** background service re-runs search
- **THEN** system compares new hash against stored hash to detect result changes

### Requirement: Wishlist items have update status
Each wishlist item SHALL have a status indicating whether results have changed.

#### Scenario: New item has Active status
- **WHEN** user creates a wishlist item
- **THEN** status is set to Active

#### Scenario: Changed results set Updated status
- **WHEN** background service detects hash mismatch
- **THEN** item status is set to Updated

### Requirement: User can navigate to search from wishlist
The system SHALL allow users to re-run the search for a wishlist item.

#### Scenario: Click navigates to search results
- **WHEN** user clicks a wishlist item in the UI
- **THEN** system sets the source dropdown and query, triggering a new search

### Requirement: Wishlist is per-user
Wishlist items SHALL be private to each user.

#### Scenario: User sees only their wishlist
- **WHEN** user requests wishlist
- **THEN** system returns only items created by that user

#### Scenario: Cannot access other users' wishlist
- **WHEN** user attempts to access wishlist item belonging to another user
- **THEN** system returns 404 Not Found