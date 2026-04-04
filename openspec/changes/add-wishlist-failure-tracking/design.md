## Context

The `WishlistBackgroundService` runs on a configurable interval and calls `WishlistService.CheckForUpdatesAsync()` to check all Active wishlist items against their sources. Currently, when a source API call fails or the source doesn't exist, the service:

1. Logs a warning
2. Skips that item
3. Continues with other items

The failure is not persisted - `WishlistItem` entity has no failure tracking fields. Users cannot see which of their wishlist items are failing or why.

## Goals / Non-Goals

**Goals:**
- Track continuous failure count per wishlist item (resets on success)
- Store last error message per wishlist item (updated on failure)
- Enable users/operators to identify problematic wishlist searches via API
- Preserve existing behavior (failures don't crash the service)

**Non-Goals:**
- Implement circuit breaker pattern (defer to future work)
- Implement automatic retry within the same interval
- Implement alerting/notification system
- Change the "Updated" status behavior when failures occur

## Decisions

### Decision 1: Add `ContinuousFailedCount` column to `WishlistItem`

**Choice**: Use an integer counter that increments on failure and resets to 0 on success.

**Rationale**: Simple, efficient, and provides immediate signal about problem items. Alternative considered was a timestamp of last failure, but a count is easier for users to interpret (e.g., "failing 5 times in a row").

**Implementation**: `public int ContinuousFailedCount { get; set; }` with default value 0.

### Decision 2: Add `LastErrorMessage` column to `WishlistItem`

**Choice**: Nullable string field, max length 1024 characters.

**Rationale**: Captures the actual error for debugging/understanding. Truncating to 1024 chars handles exception messages that could be very long while keeping column size reasonable.

**Implementation**: `public string? LastErrorMessage { get; set; }` with `[MaxLength(1024)]`.

### Decision 3: Update failure tracking on both failure AND success

**Choice**: On failure: increment count, set error message. On success: reset count to 0, clear error message.

**Rationale**: This gives the clearest signal - a non-zero count means the most recent check failed, and it failed consecutively. Clearing on success means the count represents continuous failures.

### Decision 4: `UpdatedAt` is NOT updated on failure

**Choice**: Only update `UpdatedAt` when the check succeeds.

**Rationale**: `UpdatedAt` represents "last successful check time" in the current design. If we update it on failure, we'd be lying about when the item was last successfully processed.

### Decision 5: New migration for entity changes

**Choice**: Create a new EF Core migration adding the two columns.

**Rationale**: Following existing migration pattern in the project. The migration will add columns with defaults (0 and NULL respectively).

## Risks / Trade-offs

**[Risk] Large error messages consuming storage**
→ **Mitigation**: Cap at 1024 characters. Log full message at debug/trace level in service.

**[Risk] Counter overflow for items that fail indefinitely**
→ **Mitigation**: Use `int` (max ~2 billion). Even at 1 failure per minute, this would take 4,000 years to overflow. Acceptable.

**[Risk] Existing code that serializes WishlistItem directly**
→ **Mitigation**: New fields are nullable/with defaults - no breaking changes to existing JSON serialization.

## Migration Plan

1. **Add new migration**: `dotnet ef migrations add AddWishlistFailureTracking`
2. **Deploy backend** with migration applied on startup
3. **Frontend can read new fields** via existing API (they're just additional JSON properties)
4. **No rollback needed** - if issues arise, columns can be ignored; no existing functionality depends on them

## Open Questions

1. Should there be a status transition to "Failed" after N consecutive failures? (Deferred to future - not in scope for this change)
2. Should we expose these fields in the frontend UI? (Deferred to frontend work - API is ready)
