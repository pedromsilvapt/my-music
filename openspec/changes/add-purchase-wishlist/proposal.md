## Why

Users searching for songs to purchase from external sources often don't find what they want immediately. They need a way to track search queries and be notified when results change - new songs added, removed, or reordered by the source. Currently, users must manually re-run searches to check for changes, which is tedious and error-prone.

## What Changes

- **New feature**: Wishlist system for tracking search queries from external music sources
- **New entity**: `WishlistItem` storing user's tracked searches with query text, source, and result hash
- **New background service**: Periodically re-runs wishlisted searches and marks items as "updated" when results change
- **New API endpoints**: CRUD operations for wishlist management
- **New UI**: Wishlist modal in the purchases/search UI with ability to add, view, and manage wishlist items
- **New configuration**: Settings for check interval (default 60 minutes) and max results to hash (default 50)

## Capabilities

### New Capabilities

- `wishlist-management`: Create, view, update, and delete wishlist items that track search queries from external sources
- `wishlist-background-check`: Background service that periodically checks wishlisted searches for result changes

### Modified Capabilities

(None - this is a new feature with no existing spec changes)

## Impact

**Backend:**
- New entity: `MyMusic.Common/Entities/WishlistItem.cs`
- New enum: `MyMusic.Common/Entities/WishlistItemStatus.cs`
- New service: `MyMusic.Common/Services/WishlistService.cs`
- New background service: `MyMusic.Common/Services/WishlistBackgroundService.cs`
- New controller: `MyMusic.Server/Controllers/WishlistController.cs`
- New DTOs: `MyMusic.Server/DTO/Wishlist/*.cs`
- Database migration: Add `wishlist_items` table
- Config changes: Add `WishlistCheckIntervalMinutes` and `WishlistMaxResultsToHash` settings

**Frontend:**
- New hooks: `MyMusic.Client/src/hooks/use-wishlist.ts`
- New component: `MyMusic.Client/src/components/wishlist/wishlist-modal.tsx`
- Modified: `MyMusic.Client/src/components/sources/sources-search.tsx` (add wishlist button)

**API:**
- `GET /api/wishlist` - List user's wishlist items
- `GET /api/wishlist?sourceId={id}` - Filter by source
- `POST /api/wishlist` - Create wishlist item
- `PUT /api/wishlist/{id}` - Update/reset hash (keep item)
- `DELETE /api/wishlist/{id}` - Remove from wishlist