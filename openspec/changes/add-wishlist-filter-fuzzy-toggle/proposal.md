## Why

The wishlist "Updated" status is currently unreliable because the background service and the React client use different search logic. The background service tracks hashes based on raw source results (e.g., 20 songs), while the client applies fuzzy matching and returns filtered results (e.g., 0 songs). This causes items to be incorrectly marked as "Updated" when the actual user-visible results haven't changed.

## What Changes

- **Database Schema**: Add optional `Filter` column to `WishlistItem` entity with updated unique index including Filter
- **New Service**: Create `PurchasesSearchService` to centralize search logic with optional fuzzy matching and filter DSL support
- **API Changes**:
  - Expose `fuzzyMatch` parameter on song search endpoint (default: true)
  - Add `filter` parameter support to wishlist creation
  - Update wishlist DTOs to include Filter property
- **UI Changes**:
  - Add "Show all results" toggle button below search results
  - Default to fuzzy matching when user modifies search/filter text
  - Pass current filter when creating wishlist items

## Capabilities

### New Capabilities
- `wishlist-filter-support`: Add optional Filter property to wishlist items, stored in database and included in unique index. Filter uses existing Filter DSL format.
- `purchases-search-service`: Centralized search service that both API and background service use. Supports fuzzy matching toggle and filter expression application.
- `fuzzy-match-toggle`: UI control to toggle between fuzzy-matched results (default) and raw source results.

### Modified Capabilities
- None - this change introduces new capabilities without modifying existing spec requirements.

## Impact

- **Database**: New migration required for WishlistItem.Filter column and updated unique index
- **API**: Wishlist creation endpoint accepts optional filter; Source search endpoint exposes fuzzyMatch parameter
- **Services**: WishlistService refactored to use PurchasesSearchService instead of direct source calls
- **UI**: Search results page adds fuzzy toggle button; Wishlist modal passes filter on creation
- **Backward Compatibility**: All new parameters are optional with sensible defaults - existing functionality preserved
