## Context

The MyMusic application manages playlists with songs that users can play in sequence. The player system currently advances automatically through the queue without stopping. Users want the ability to mark specific songs to pause playback after they finish, with this preference persisted across sessions.

Current state:
- Playlist songs are stored in the database with metadata (order, added date, etc.)
- Playback is controlled through Zustand stores that sync with the current playlist
- Collection actions are defined via schemas that receive selected items data
- The collection actions function receives selected items with their properties

Constraints:
- Must integrate with existing entity framework models
- Must follow existing DTO patterns (separate request/response files)
- Must support API endpoints for CRUD operations on the flag
- Must persist state across page refreshes (database storage)
- Must integrate with collection actions schema system

## Goals / Non-Goals

**Goals:**
- Allow users to mark any song in a playlist to stop playback after it finishes
- Store the stop-after flag in the database (playlist_songs table) for persistence
- Pause playback automatically when a marked song completes
- Provide clear visual indication of which songs are marked with stop-after flag
- Allow the action to work with the collection actions schema (derived from selected items)
- Clean up the flag automatically once it triggers

**Non-Goals:**
- Supporting stop-after for non-playlist queues (temporary queues)
- Server-side playback control (client handles the pause logic)

## Decisions

### 1. Store stop-after flag in database (playlist_songs table)
**Decision:** Add a `StopAfterPlayback` boolean column to the `playlist_songs` table.
**Rationale:** The flag must persist across sessions. Each playlist entry can have its own independent flag, allowing the same song in different playlists (or different positions in the same playlist) to have different stop settings.

**Alternatives considered:**
- Store in user preferences table: Rejected because it's tied to specific playlist entries, not global user preferences
- Store only in client state: Rejected because users want persistence across refreshes

### 2. Use collection actions schema for UI control
**Decision:** The collection actions schema will include `StopAfterPlayback` in the selected items data, allowing the action to render conditionally based on the selected songs' states.
**Rationale:** Leverages existing infrastructure. The action can show a checkmark if the selected items have the flag, and toggle behavior can work for both single and multiple selections consistently.

**Alternatives considered:**
- Separate API call to check flag status: Rejected as it adds unnecessary complexity when the data can be included in selected items
- Client-only state management: Rejected due to persistence requirements

### 3. Client-side playback pause with server flag clear
**Decision:** When a song with `StopAfterPlayback=true` finishes, the client pauses playback and calls the API to clear the flag.
**Rationale:** Playback control is inherently client-side. The flag clear must be persisted to the server for consistency.

### 4. Visual indicator using Mantine Badge or custom icon
**Decision:** Use a small stop icon (from @tabler/icons-react) next to songs marked with the flag, styled with Mantine's theme colors.
**Rationale:** Consistent with existing UI patterns, immediately recognizable, doesn't clutter the interface.

### 5. Batch API endpoint for multi-select efficiency
**Decision:** Provide a batch endpoint to update the `StopAfterPlayback` flag for multiple songs in a single request.
**Rationale:** Multi-select is a common use case. Making N API calls for N songs causes unnecessary network overhead. A batch endpoint enables atomic updates and better performance.

**Alternatives considered:**
- Individual endpoint only: Rejected due to N+1 query problem when updating multiple songs
- Client-side batching wrapper: Rejected as it still makes multiple network requests

## Risks / Trade-offs

**[Risk]** User confusion about why playback stopped
→ **Mitigation:** Show a brief toast notification or status indicator explaining "Playback stopped after [Song Name]" when the flag triggers.

**[Risk]** API lag when clearing flag after song ends
→ **Mitigation:** Optimistically clear the flag in client state immediately, sync with server asynchronously.

**[Risk]** Flag on last song causes confusion (nothing to stop after)
→ **Mitigation:** Allow the flag but handle gracefully - if it's the last song, pause at the end as expected.

**[Trade-off]** Database schema change requires migration
→ **Acceptance:** This is a one-time cost for persistence benefits.

## Migration Plan

1. Create EF Core migration to add `stop_after_playback` column to `playlist_songs` table
2. Apply migration to development database
3. Deploy code that can handle both old (no column) and new states gracefully
4. Apply migration to production database during maintenance window

## Open Questions

None at this time.
