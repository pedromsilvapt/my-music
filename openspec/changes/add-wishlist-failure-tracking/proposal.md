## Why

The current wishlist background check service silently ignores failures - when a source is unreachable, returns an error, or no longer exists, the system logs a warning but provides no way for users or operators to:
1. Know that a wishlist item is consistently failing
2. Understand WHY it failed (no error message persisted)
3. Take corrective action (source deleted, API rate-limited, network issues)

Without persistent failure tracking, users may not realize their wishlist searches are no longer being executed, leading to silent data loss in the "updated" status detection.

## What Changes

- **Add `ContinuousFailedCount` to `WishlistItem` entity**: Integer counter that increments each time a background check fails for that item, and resets to 0 on success.
- **Add `LastErrorMessage` to `WishlistItem` entity**: String field that stores the most recent error message (truncated if needed) when a check fails.
- **Update `CheckForUpdatesAsync`**: Increment failure count and set error message on exceptions; reset count and clear error message on success.
- **Update background service spec**: Document new failure tracking behavior.
- **Update management spec**: Document that users can see failure state via API.

## Capabilities

### New Capabilities

- `wishlist-failure-tracking`: Ability to track continuous failure count and last error message for each wishlist item, enabling users and operators to identify problematic wishlist searches.

### Modified Capabilities

- `wishlist-background-check`: The background service behavior changes to track failures persistently rather than just logging them. The requirements around error handling are updated.

## Impact

- **Database**: `WishlistItem` table gets two new columns: `ContinuousFailedCount` (integer, default 0) and `LastErrorMessage` (string, nullable).
- **Backend**: `WishlistService.CheckForUpdatesAsync` modified to update failure tracking fields. New migration required.
- **No breaking changes**: Existing behavior preserved - failures still don't crash the service. New fields are additive.
