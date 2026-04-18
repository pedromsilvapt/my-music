## Why

When adding songs that are already in the queue using "Play Next" or "Play Last", the frontend optimistic update duplicates the songs instead of moving them. The backend correctly moves existing songs to the new position, but the frontend displays duplicates until a page refresh or navigation triggers a refetch. This creates a confusing UX where the same song appears in multiple positions.

## What Changes

- Fix `playNext()` optimistic update to remove songs from their current positions before inserting at new position
- Fix `playLast()` optimistic update to remove songs from their current positions before appending to end
- Both functions will now match backend behavior: existing songs are moved, new songs are added

## Capabilities

### New Capabilities

- `queue-optimistic-update`: Correctly handles optimistic updates for queue mutations, ensuring songs already in the queue are moved (not duplicated) when using Play Next or Play Last actions

### Modified Capabilities

(none - this is a bug fix with no requirement changes)

## Impact

- **Frontend**: `MyMusic.Client/src/hooks/use-queue.ts` - `playNext()` and `playLast()` functions
- **UX**: Users will see accurate queue state immediately after Play Next/Play Last actions
- **No backend changes required** - backend already handles this correctly
