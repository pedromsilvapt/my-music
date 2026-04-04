## 1. Backend: Entity and Database

- [x] 1.1 Create `WishlistItemStatus` enum in `MyMusic.Common/Entities/WishlistItemStatus.cs`
- [x] 1.2 Create `WishlistItem` entity in `MyMusic.Common/Entities/WishlistItem.cs`
- [x] 1.3 Add `WishlistItems` DbSet to `MusicDbContext`
- [x] 1.4 Add `WishlistCheckIntervalMinutes` and `WishlistMaxResultsToHash` to `Config.cs`
- [x] 1.5 Create database migration: `dotnet ef migrations add AddWishlistItem`

## 2. Backend: Wishlist Service

- [x] 2.1 Create `IWishlistService` interface in `MyMusic.Common/Services/IWishlistService.cs`
- [x] 2.2 Implement `WishlistService` in `MyMusic.Common/Services/WishlistService.cs` with:
  - `CreateAsync(userId, sourceId, query, songIds)`
  - `ListAsync(userId, sourceId?)`
  - `UpdateHashAsync(id)`
  - `DeleteAsync(id)`
  - `CheckForUpdatesAsync()` (for background service)
- [x] 2.3 Implement hash computation method (SHA256 of sorted source song IDs)

## 3. Backend: Background Service

- [x] 3.1 Create `WishlistBackgroundService` in `MyMusic.Common/Services/WishlistBackgroundService.cs`
- [x] 3.2 Implement `ExecuteAsync` with configurable interval (default 60 minutes)
- [x] 3.3 Add 30-second delay on startup before first check
- [x] 3.4 Implement iteration over Active wishlist items
- [x] 3.5 Implement hash comparison and status update logic
- [x] 3.6 Add error handling for source API failures

## 4. Backend: DTOs

- [x] 4.1 Create `MyMusic.Server/DTO/Wishlist/CreateWishlistRequest.cs`
- [x] 4.2 Create `MyMusic.Server/DTO/Wishlist/CreateWishlistResponse.cs`
- [x] 4.3 Create `MyMusic.Server/DTO/Wishlist/ListWishlistResponse.cs`
- [x] 4.4 Create `MyMusic.Server/DTO/Wishlist/WishlistItem.cs` (record with FromEntity mapping)
- [x] 4.5 Create `MyMusic.Server/DTO/Wishlist/UpdateWishlistResponse.cs`

## 5. Backend: Controller and DI

- [x] 5.1 Create `WishlistController` in `MyMusic.Server/Controllers/WishlistController.cs`
- [x] 5.2 Implement `GET /api/wishlist` endpoint
- [x] 5.3 Implement `GET /api/wishlist?sourceId={id}` endpoint
- [x] 5.4 Implement `POST /api/wishlist` endpoint
- [x] 5.5 Implement `PUT /api/wishlist/{id}` endpoint
- [x] 5.6 Implement `DELETE /api/wishlist/{id}` endpoint
- [x] 5.7 Register `WishlistService` and `WishlistBackgroundService` in DI

## 6. Backend: Testing

- [x] 6.1 Create `WishlistServiceSpecs` test class in `MyMusic.Common.Tests/Services/`
- [x] 6.2 Write test: `CreateWishlistItem_ValidData_CreatesItemWithCorrectHash`
- [x] 6.3 Write test: `CheckForUpdates_HashChanged_MarksAsUpdated`
- [x] 6.4 Write test: `CheckForUpdates_HashUnchanged_KeepsActive`
- [x] 6.5 Write test: `UpdateWishlistItem_ResetsHashAndStatus`
- [x] 6.6 Write test: `DeleteWishlistItem_RemovesFromDatabase`
- [x] 6.7 Run all tests: `dotnet test`

## 7. Frontend: API Integration

- [x] 7.1 Start server to update OpenAPI spec
- [x] 7.2 Run `devbox run orval` to regenerate client hooks

## 8. Frontend: Hooks

- [x] 8.1 Create `MyMusic.Client/src/hooks/use-wishlist.ts`
- [x] 8.2 Export `useWishlist(sourceId?)` - uses generated hook with optional filter
- [x] 8.3 Export `useCreateWishlist()` - wraps generated mutation with invalidation
- [x] 8.4 Export `useUpdateWishlist()` - wraps generated mutation with invalidation
- [x] 8.5 Export `useRemoveWishlist()` - wraps generated mutation with invalidation

## 9. Frontend: Wishlist Modal Component

- [x] 9.1 Create `MyMusic.Client/src/components/wishlist/` directory
- [x] 9.2 Create `wishlist-modal.tsx` with Mantine Modal component
- [x] 9.3 Implement wishlist items list with source icon, query, status badge
- [x] 9.4 Implement click handler to navigate to search (set source + query)
- [x] 9.5 Implement "Keep" button for Updated items (calls updateWishlist)
- [x] 9.6 Implement "Remove" button with confirmation
- [x] 9.7 Implement "Add current search" button (visible when search has results)
- [x] 9.8 Add styles imports to `src/components/styles.ts` if needed

## 10. Frontend: Integration with Sources Search

- [x] 10.1 Add wishlist icon button to `sources-search.tsx` toolbar
- [x] 10.2 Implement modal open/close state with `useDisclosure`
- [x] 10.3 Pass current source, query, and song IDs to modal
- [x] 10.4 Handle wishlist item click to update search state

## 11. Final Verification

- [x] 11.1 Run backend build: `dotnet build`
- [x] 11.2 Run backend tests: `dotnet test`
- [x] 11.3 Run frontend lint: `npm run lint`
- [ ] 11.4 Manual testing: Create wishlist item from search
- [ ] 11.5 Manual testing: Verify background service marks items as Updated
- [ ] 11.6 Manual testing: Click item navigates to search
- [ ] 11.7 Manual testing: Keep button updates hash and resets status
- [ ] 11.8 Manual testing: Delete removes from list