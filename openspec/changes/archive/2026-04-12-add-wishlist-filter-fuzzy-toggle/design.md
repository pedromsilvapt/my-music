## Context

The wishlist background service (`WishlistBackgroundService`) periodically checks active wishlist items to detect if search results have changed. It computes a hash of song IDs from the source search results and compares it to the stored hash. If different, the item is marked as "Updated".

However, the React client applies additional fuzzy filtering to source search results before displaying them to users. This creates a mismatch:
- Background service: Computes hash from raw source results (e.g., 20 songs)
- Client: Computes hash from fuzzy-filtered results (e.g., 0 songs)

This causes false "Updated" notifications when the raw results change but the filtered results remain the same (or vice versa).

Current architecture:
- `WishlistService` directly calls `ISourcesService.GetSourceClientAsync()` and `ISource.SearchSongsAsync()`
- `SourcesController.SearchSongsAsync()` applies `InMemoryFilterBuilder.ApplyFuzzySearch()` after getting raw results
- No shared logic between these code paths

## Goals / Non-Goals

**Goals:**
- Create centralized `PurchasesSearchService` used by both API and background service
- Add optional `Filter` property to wishlist items with database persistence
- Ensure wishlist hash computation uses same logic as client-visible results
- Add UI toggle for fuzzy matching with sensible defaults
- Maintain backward compatibility (all new parameters optional)

**Non-Goals:**
- Changing the fuzzy matching algorithm itself
- Adding new filter DSL operators
- Modifying the thumbnail proxy behavior
- Supporting multiple filters per wishlist item
- Real-time wishlist updates (still periodic background checks)

## Decisions

**Decision: PurchasesSearchService owns all search logic**
- Rationale: Centralizes fuzzy matching and filter application in one place
- Alternative considered: Shared helper class with static methods - rejected because service needs DI dependencies (ISourcesService, logging)
- Implementation: Interface + Implementation in MyMusic.Common for accessibility from both MyMusic.Server and wishlist service

**Decision: Filter property is stored as raw Filter DSL string**
- Rationale: Reuses existing `FilterDslParser` infrastructure
- Alternative considered: Parsed expression tree storage - rejected due to complexity and serialization issues
- Migration: NULL means no filter (existing behavior)

**Decision: fuzzyMatch defaults to true in API, always true for wishlist**
- Rationale: Matches current client behavior; wishlist should track what users actually see
- The toggle allows users to see raw results for exploration, but wishlist tracking uses fuzzy-matched results

**Decision: Unique index includes Filter column**
- Rationale: Same query with different filters are distinct tracking items
- Implementation: Updated `[Index]` attribute on WishlistItem entity

## Risks / Trade-offs

**Risk**: Database migration fails on existing data with duplicate (OwnerId, SourceId, Query) combinations
→ **Mitigation**: Filter column is nullable; unique index treats NULL as distinct. Existing items have NULL filter, so no duplicates created.

**Risk**: Performance impact of parsing Filter DSL on every background check
→ **Mitigation**: Filter DSL parsing is fast (string-based). Caching at source level (`CachedSource`) still applies to raw results before filtering.

**Risk**: UI complexity from fuzzy toggle confusion
→ **Mitigation**: Clear button label "Show all results" / "Show matched results". Default fuzzy=true maintains current behavior.

**Trade-off**: Wishlist always uses fuzzy=true even if user created it while viewing raw results
→ **Acceptable**: User can delete and recreate if they want to track raw results. Most users want to track what matches their query.

## Migration Plan

1. **Database**: Run `dotnet ef migrations add AddWishlistFilter`
2. **Services**: Deploy new `PurchasesSearchService`, update `WishlistService` dependency injection
3. **API**: Deploy controller changes (backward compatible - new parameters optional)
4. **UI**: Deploy React changes with Orval regenerated client
5. **Verification**: Check that existing wishlist items continue to work (NULL filter)

Rollback: Revert migrations, code. Existing wishlist data preserved (Filter column ignored by old code).

## Open Questions

None - all decisions resolved in planning phase.
