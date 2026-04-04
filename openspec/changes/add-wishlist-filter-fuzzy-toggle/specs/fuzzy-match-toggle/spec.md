## ADDED Requirements

### Requirement: Search endpoint exposes fuzzyMatch parameter
The system SHALL expose a fuzzyMatch query parameter on the song search endpoint, defaulting to true.

#### Scenario: Search with default fuzzy matching
- **WHEN** client calls GET /api/sources/{id}/songs/search/{query} without fuzzyMatch parameter
- **THEN** results are filtered using fuzzy matching (as if fuzzyMatch=true)

#### Scenario: Search with explicit fuzzy matching disabled
- **WHEN** client calls GET /api/sources/{id}/songs/search/{query}?fuzzyMatch=false
- **THEN** raw source results are returned without fuzzy filtering

#### Scenario: Search with explicit fuzzy matching enabled
- **WHEN** client calls GET /api/sources/{id}/songs/search/{query}?fuzzyMatch=true
- **THEN** results are filtered using fuzzy matching

### Requirement: UI defaults to fuzzy matching on search modification
The system SHALL reset fuzzyMatch to true when the user modifies the search query or filter text.

#### Scenario: User types new search query
- **GIVEN** user is viewing results with fuzzyMatch=false (showing all results)
- **WHEN** user types a new character in the search box
- **THEN** fuzzyMatch automatically resets to true
- **AND** new search executes with fuzzy matching enabled

#### Scenario: User modifies filter expression
- **GIVEN** user is viewing results with fuzzyMatch=false
- **WHEN** user modifies the filter text
- **THEN** fuzzyMatch automatically resets to true

### Requirement: UI provides toggle button below results
The system SHALL display a toggle button below the search results list to switch between fuzzy-matched and all results.

#### Scenario: Button shows correct state when fuzzy matching is active
- **GIVEN** fuzzyMatch is true (default)
- **THEN** button displays "Show all results"
- **AND** clicking it sets fuzzyMatch to false and refreshes results

#### Scenario: Button shows correct state when showing all results
- **GIVEN** fuzzyMatch is false (user clicked to show all)
- **THEN** button displays "Show matched results"
- **AND** clicking it sets fuzzyMatch to true and refreshes results

#### Scenario: Button is positioned below results
- **WHEN** search results are displayed
- **THEN** the toggle button appears below the list/grid of results
- **AND** it is clearly separated from the results content

### Requirement: Wishlist creation captures current filter but not fuzzy state
The system SHALL store the current filter expression when creating a wishlist item, but always use fuzzyMatch=true for wishlist tracking regardless of current UI state.

#### Scenario: Creating wishlist from filtered search
- **GIVEN** user has searched with query "test" and filter "genre:Pop"
- **WHEN** user adds current search to wishlist
- **THEN** the wishlist item stores Filter="genre:Pop"
- **AND** future checks use fuzzyMatch=true with that filter

#### Scenario: Creating wishlist while viewing all results
- **GIVEN** user is viewing all results (fuzzyMatch=false) with query "test"
- **WHEN** user adds current search to wishlist
- **THEN** the wishlist item stores Filter=NULL
- **AND** future checks still use fuzzyMatch=true for consistent tracking
