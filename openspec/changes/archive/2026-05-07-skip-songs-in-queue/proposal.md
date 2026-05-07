## Why

Users listening to a queue sometimes want to temporarily skip one or more upcoming songs without removing them from the queue. Currently the only options are to remove songs entirely or manually skip past them each time they come up. A one-time "skip" flag lets users defer songs for the current listening session while keeping them in the queue for future plays.

## What Changes

- Add a `SkipNextPlayback` boolean flag to `PlaylistSong` (backend entity, migration, DTOs) — modeled after the existing `StopAfterPlayback` flag
- Add API endpoints for setting/clearing the skip flag on individual and batch playlist songs, returning proper HTTP status codes (400/404/403) and delegating business logic to services
- Validate batch endpoint inputs: reject empty `songIds` and song IDs not present in the specified playlist (return 400 Bad Request)
- Modify the next-song calculation (`goForward`) to skip over songs that have the `SkipNextPlayback` flag set, using index-based iteration on the sorted queue array
- When playback skips past a flagged song, automatically clear the `SkipNextPlayback` flag on that song (one-time behavior), using the `clearSkipNextPlayback` callback with optimistic updates and rollback. Flags are also cleared when all remaining songs are flagged (the songs were logically skipped past even though no next song was found).
- Clear the `SkipNextPlayback` flag on the current song when playback **starts** (if the song is played despite being flagged, the flag is consumed immediately)
- Enforce mutual exclusivity: setting `SkipNextPlayback=true` forces `StopAfterPlayback=false`, and setting `StopAfterPlayback=true` forces `SkipNextPlayback=false`. Only applies when setting a flag to `true`; setting to `false` does not affect the other flag.
- Add a visual indicator (icon `player-skip-forward`, gray when inactive, yellow/orange when active) on songs marked as skipped in the now playing queue
- Add a collection actions menu item to toggle the skip flag on selected songs
- Notify the user when playback stops because all remaining songs in the queue are flagged to skip
- Rename from `SkipAfterPlayback` to `SkipNextPlayback` across all layers (entity, DB column, DTOs, API paths, TypeScript types, frontend code) to better reflect the one-time behavior
- Handle edge cases: skipping past the last song, all remaining songs flagged, interaction with `StopAfterPlayback`

## Capabilities

### New Capabilities
- `skip-songs-in-queue`: One-time skip flag on playlist songs that causes the next-song calculation to skip them, then auto-clears the flag. Mutually exclusive with `StopAfterPlayback`.

### Modified Capabilities
- `stop-after-song`: The next-song calculation in the playback finish handler must now also consider the `SkipNextPlayback` flag when determining which song to advance to. Setting `StopAfterPlayback=true` now also clears `SkipNextPlayback` on the same song.

## Impact

- **Backend**: Rename `skip_after_playback` → `skip_next_playback` column (migration); new service methods for skip operations with proper HTTP status codes (400/404/403), mutual exclusivity logic, and batch validation; updated `GetPlaylistSongItem` DTO; new request DTOs; update stop-after endpoints to clear `SkipNextPlayback`
- **Frontend**: Rename all `skipAfterPlayback`/`SkipAfterPlayback` references to `skipNextPlayback`/`SkipNextPlayback`; updated player navigation hook (`use-player-navigation.ts`) to skip flagged songs on `goForward` using index-based iteration; clear skip flag on current song when playback starts; use `clearSkipNextPlayback` with optimistic updates from `useQueueMutations`; update `toggleSkipNextPlayback` and `toggleStopAfterPlayback` to handle mutual exclusivity (optimistically clear the other flag when setting one to true); notify user when all remaining songs are flagged; new collection action in `useSongsSchema.tsx`; new visual indicator in `song-title.tsx`; optimistic updates in `use-queue.ts`; Orval config updates for new mutation names
- **Orval**: New mutation invalidation entries for skip-related endpoints (renamed)
- **Existing data**: Migration renames column; no data loss
