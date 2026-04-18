## Context

The queue management system uses optimistic updates for a responsive UI. When users add songs via "Play Next" or "Play Last", the frontend updates the React Query cache immediately before the server responds.

**Current State:**
- Backend `AddToQueue` endpoint correctly handles duplicate songs by moving them (not duplicating)
- Frontend `playNext()` and `playLast()` simply insert songs without checking if they already exist in the queue
- This mismatch causes UI to show duplicates until `invalidateQueries` triggers a refetch

**Architecture:**
```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Current Flow                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  User selects songs → Play Next/Last                                       │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │ Frontend (use-queue.ts)                                              │   │
│  │                                                                      │   │
│  │  playNext/playLast:                                                  │   │
│  │    1. Get current queue from cache                                   │   │
│  │    2. Create new playlist song objects                               │   │
│  │    3. Insert at position (after current or at end)                   │   │
│  │    4. setQueryData with optimistic queue  ← BUG: doesn't remove     │   │
│  │    5. mutate() to server                                             │   │
│  │    6. onSettled: invalidateQueries                                   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │ Backend (PlaylistsController.cs)                                     │   │
│  │                                                                      │   │
│  │  AddToQueue:                                                         │   │
│  │    1. Identify existing songs in queue                               │   │
│  │    2. Identify new songs                                             │   │
│  │    3. Remove existing songs from old positions                       │   │
│  │    4. Insert all songs at new position                               │   │
│  │    5. Return updated queue                                           │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Goals / Non-Goals

**Goals:**
- Make `playNext()` optimistic update match backend behavior (move existing songs)
- Make `playLast()` optimistic update match backend behavior (move existing songs)
- Ensure UI displays correct queue state immediately after action

**Non-Goals:**
- Changes to backend behavior (already correct)
- Changes to other queue mutations (`play`, `reorder`, `remove`, etc. - these don't have the bug)
- Adding new queue features

## Decisions

### Decision 1: Filter-then-insert approach

**Chosen:** Filter existing songs from queue before inserting at new position

**Rationale:** This matches the backend logic exactly:
1. Create a Set of songIds being added
2. Filter the current queue to exclude those songs
3. Insert the (now non-duplicate) songs at the target position

**Alternatives considered:**
- Remove-then-append separately: More complex, two operations on queue
- Let backend handle it, remove optimistic update entirely: Poor UX, no instant feedback
- Track song positions and move them: Overly complex for a simple operation

### Decision 2: Shared helper function

**Chosen:** Create a `filterOutExistingSongs()` helper used by both `playNext` and `playLast`

**Rationale:**
- Both functions have the same bug with the same fix
- Single helper reduces code duplication
- Makes the intent clear: "remove existing songs before adding"

## Risks / Trade-offs

**Risk:** Incorrect filter logic could remove wrong songs
→ **Mitigation:** Filter by song ID, which is unique. Add unit tests.

**Risk:** Race conditions if multiple queue operations happen simultaneously
→ **Mitigation:** Existing `onError` rollback handles this. No change needed.

**Trade-off:** Slightly more computation (filter operation)
→ **Acceptable:** O(n) filter is negligible for typical queue sizes (< 1000 songs)
