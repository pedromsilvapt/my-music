## Context

MyMusic allows users to search for songs from external music sources (e.g., music stores, streaming APIs) and purchase them. When a search doesn't yield desired results, users currently have no way to track that query for future changes. This design introduces a wishlist feature to bridge that gap.

**Current Architecture:**
- `Source` entities represent external music providers with REST APIs
- `SourcesService` creates Refit clients (`ISource`) to call source APIs
- `PurchasesQueue` background service processes song purchases asynchronously
- Frontend uses `sources-search.tsx` component for searching across sources

**Constraints:**
- Must integrate with existing `ISource` interface for re-running searches
- Must be per-user (private wishlists)
- Must not impact performance of active searches or purchases
- Should use existing patterns (BackgroundService, EF Core, Orval-generated hooks)

## Goals / Non-Goals

**Goals:**
- Allow users to save search queries with result fingerprints (hashes)
- Automatically detect when search results change (new songs, removed songs, reordering)
- Provide a lightweight UI to manage wishlisted searches
- Make configuration tunable via existing Config system

**Non-Goals:**
- No push notifications or real-time alerts (visual badge only)
- No partial result tracking (only hash of top N results)
- No diff visualization (just "changed" status, not what changed)
- No wishlist sharing between users

## Decisions

### 1. Hash Strategy: SHA256 of Sorted Source Song IDs

**Decision:** Hash only the source's song IDs (external IDs), not full metadata.

**Rationale:**
- Source IDs are stable identifiers provided by external APIs
- Hashing full metadata would be noisy (minor title changes, etc.)
- SHA256 is cryptographically strong and fast for small inputs
- Sorted order ensures consistent hashes regardless of result order

**Alternative Considered:** Hash full JSON response
- Rejected: Too sensitive to irrelevant changes (e.g., metadata updates)
- Would cause excessive "updated" flags for non-meaningful changes

### 2. Background Service Architecture: Single-Pass Batch Check

**Decision:** Use a single background service that iterates all Active wishlist items on a configurable interval.

**Rationale:**
- Simple, predictable behavior
- Matches existing pattern used by `PurchasesQueue`
- Interval-based (not per-item scheduling) keeps implementation straightforward
- Configurable via `WishlistCheckIntervalMinutes` (default: 60)

**Alternative Considered:** Per-item scheduling with last-checked timestamps
- Rejected: More complex, requires tracking per-item check times
- Not necessary for typical use case (users don't need sub-minute updates)

### 3. Result Limiting: Top N Results

**Decision:** Only hash the first N results (configurable via `WishlistMaxResultsToHash`, default: 50).

**Rationale:**
- Sources may return thousands of results; hashing all is wasteful
- Users typically care about top results
- Prevents unbounded hash sizes and processing time

**Alternative Considered:** Hash all results
- Rejected: Could cause performance issues with large result sets
- Unnecessary for typical wishlist use case

### 4. UI Placement: Wishlist Button in Search Toolbar

**Decision:** Add a wishlist icon button in `sources-search.tsx` toolbar, opening a modal with wishlist items.

**Rationale:**
- Keeps wishlist contextually close to where searches happen
- Modal pattern matches existing UI patterns (configuration dialogs)
- "Add current search" button visible when search has results

**Alternative Considered:** Separate route at `/wishlist`
- Rejected: Less contextual, requires navigation away from search
- Users think of wishlist in the context of searching, not as a separate page

### 5. Click Behavior: Navigate to Search Results

**Decision:** Clicking a wishlist item sets the source dropdown and query, triggering a new search.

**Rationale:**
- Immediate feedback: show user what changed
- Reuses existing search flow
- No need for separate "view results" state

## Risks / Trade-offs

### Risk: External Source Rate Limiting
**Risk:** Background service making frequent searches could hit source API rate limits.
**Mitigation:** Configurable interval (default 60 min) reduces frequency. Document that users should set appropriate intervals based on source limits.

### Risk: Stale Results After "Keep"
**Risk:** User clicks "Keep" to update hash, but results change again soon after.
**Mitigation:** This is expected behavior - the cycle repeats. If user wants continuous monitoring, they keep the item in wishlist.

### Risk: Large Number of Wishlist Items
**Risk:** User creates many wishlist items, causing long background processing times.
**Mitigation:** Process batched in single pass; if scalability becomes issue, add pagination or priority queue.

### Risk: Source API Changes
**Risk:** External source API changes format, breaking hash comparisons.
**Mitigation:** Hash uses source's song IDs which are typically stable. If source API fundamentally changes, the service layer (`ISource`) handles adaptation.

## Migration Plan

1. Deploy backend changes (entity, service, controller, background service)
2. Run database migration to create `wishlist_items` table
3. Deploy frontend changes (hooks, modal, search integration)
4. Monitor background service logs for any API errors

**Rollback:**
- Stop background service via config (set interval to 0 or high value)
- Migration can be reversed via EF Core migration rollback
- Frontend gracefully handles empty wishlist API responses

## Open Questions

(None - requirements clarified through user conversation)