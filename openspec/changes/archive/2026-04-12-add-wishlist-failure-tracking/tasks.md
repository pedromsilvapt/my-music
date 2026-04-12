## 1. Database Migration

- [x] 1.1 Add `ContinuousFailedCount` column to `WishlistItem` entity (int, default 0)
- [x] 1.2 Add `LastErrorMessage` column to `WishlistItem` entity (string, nullable, max 1024)
- [x] 1.3 Create EF Core migration `dotnet ef migrations add AddWishlistFailureTracking`
- [x] 1.4 Verify migration applies correctly

## 2. Backend Service Updates

- [x] 2.1 Update `WishlistService.CheckForUpdatesAsync` to increment `ContinuousFailedCount` on failure
- [x] 2.2 Update `WishlistService.CheckForUpdatesAsync` to set `LastErrorMessage` on failure (truncate to 1024 chars)
- [x] 2.3 Update `WishlistService.CheckForUpdatesAsync` to reset `ContinuousFailedCount` to 0 on success
- [x] 2.4 Update `WishlistService.CheckForUpdatesAsync` to clear `LastErrorMessage` (set to NULL) on success
- [x] 2.5 Ensure `UpdatedAt` is NOT updated on failure (only on success)
- [x] 2.6 Run tests to verify failure tracking behavior

## 3. Verification

- [x] 3.1 Run existing wishlist tests to ensure no regressions
- [x] 3.2 Add unit tests for failure tracking in `WishlistServiceSpecs`
- [x] 3.3 Verify API responses include new fields
