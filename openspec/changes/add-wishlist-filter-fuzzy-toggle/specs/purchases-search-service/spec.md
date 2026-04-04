## ADDED Requirements

### Requirement: PurchasesSearchService provides unified search
The system SHALL provide a PurchasesSearchService that centralizes search logic for both API and background service consumption.

#### Scenario: Service returns raw results without fuzzy matching
- **GIVEN** a source returns 20 songs for query "test"
- **WHEN** PurchasesSearchService.SearchAsync is called with fuzzyMatch=false
- **THEN** all 20 songs are returned without fuzzy filtering

#### Scenario: Service returns fuzzy-matched results
- **GIVEN** a source returns 20 songs for query "Holly Humberstone Embers in the sky"
- **AND** only 5 songs contain all search terms in their SearchableText
- **WHEN** PurchasesSearchService.SearchAsync is called with fuzzyMatch=true
- **THEN** only those 5 matching songs are returned

#### Scenario: Service applies filter DSL expression
- **GIVEN** a source returns songs of various genres
- **WHEN** PurchasesSearchService.SearchAsync is called with filter="genre:Pop" and fuzzyMatch=true
- **THEN** results are first fuzzy-matched against query, then filtered to Pop genre only

#### Scenario: Service applies filter without fuzzy matching
- **GIVEN** a source returns songs of various years
- **WHEN** PurchasesSearchService.SearchAsync is called with filter="year:2023" and fuzzyMatch=false
- **THEN** all songs from 2023 are returned regardless of fuzzy match against query

### Requirement: Service is accessible from both Server and Common projects
The system SHALL register PurchasesSearchService in the DI container for use by both MyMusic.Server controllers and MyMusic.Common services.

#### Scenario: Controller uses service
- **WHEN** SourcesController.SearchSongsAsync handles a request
- **THEN** it calls PurchasesSearchService.SearchAsync instead of direct source calls

#### Scenario: WishlistService uses service
- **WHEN** WishlistService.CheckForUpdatesAsync processes an item
- **THEN** it calls PurchasesSearchService.SearchAsync with the item's filter and fuzzyMatch=true

### Requirement: Service logs search operations
The system SHALL log detailed information about each search operation for debugging.

#### Scenario: Search with fuzzy matching logs filtering
- **WHEN** PurchasesSearchService.SearchAsync is called with fuzzyMatch=true
- **THEN** logs show raw result count and post-fuzzy-match count
- **AND** logs show filter application if filter is provided
