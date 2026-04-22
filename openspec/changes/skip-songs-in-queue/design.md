## Context

The now-playing queue stores songs as `PlaylistSong` entities with an `Order` field and a `StopAfterPlayback` flag. When a song finishes, `goForward()` in `use-player-navigation.ts` selects the next song by `order === currentIndex + 1`. The `StopAfterPlayback` flag causes the finish handler to pause instead of advancing.

The new `SkipNextPlayback` flag follows the same storage pattern (boolean column on `playlist_songs`) but with different runtime semantics: it causes the next-song calculation to skip past flagged songs rather than pause on them, and it auto-clears after being "consumed" (i.e., after playback jumps past the song). The name `SkipNextPlayback` (rather than `SkipAfterPlayback`) more accurately reflects the one-time behavior: the song will be skipped on the **next** playback cycle, after which the flag is consumed.

## Goals / Non-Goals

**Goals:**
- Add a persistent `SkipNextPlayback` flag on `PlaylistSong`, identical in storage to `StopAfterPlayback`
- Provide single and batch API endpoints to set/clear the flag
- Modify `goForward()` to find the next non-skipped song in the queue
- Auto-clear the `SkipNextPlayback` flag on songs that are skipped past during normal playback progression
- Provide a visual indicator (icon `player-skip-forward`) in the now-playing queue
- Add a collection action to toggle the skip flag
- Handle edge cases: all remaining songs flagged, last song flagged, interaction with StopAfterPlayback
- Enforce mutual exclusivity: setting `SkipNextPlayback=true` clears `StopAfterPlayback`, and vice versa (only when setting to `true`)
- Validate batch endpoint inputs: reject empty `songIds` and song IDs not in the playlist

**Non-Goals:**
- Skipping songs via a manual "next" button press (user explicitly skips — this already works and doesn't consume the flag)
- Changing how `StopAfterPlayback` works (it pauses playback and clears the flag on the current song; no behavior change)
- Removing songs from the queue (skipping keeps them in the queue)
- Skip counts or statistics

## Decisions

### 1. Store `SkipNextPlayback` as a boolean column on `playlist_songs`

**Rationale**: Mirrors the proven `StopAfterPlayback` pattern. Same entity, same migration approach, same DTO mapping. No new tables needed.

**Alternative considered**: A separate `SongFlags` table with flag type and value. Rejected because it adds complexity for a single boolean and doesn't align with the existing pattern.

### 2. Reuse the batch endpoint pattern for API

**Rationale**: Follow the existing `SetStopAfterPlayback` / `BatchSetStopAfterPlayback` pattern exactly — single-song endpoint at `PUT /playlists/{id}/songs/{songId}/skip-next-playback` and batch at `PUT /playlists/songs/skip-next-playback/batch`. Same ownership verification, same response shape.

### 3. Modify `goForward()` to skip past flagged songs

**Rationale**: `goForward()` currently finds `queue.find(s => s.order === currentIndex + 1)`. Instead, it should iterate forward through the queue, skipping any song with `skipNextPlayback === true`, and land on the first non-skipped song. Since the server returns songs sorted by `Order` in the DTO mapping (`GetPlaylistItem.FromEntity`), the frontend `queue` array is already sorted — iteration should use array indices directly rather than searching by `order` property.

**Alternative considered**: Only skip in the `handleFinish` path (auto-advance) but not in manual `goForward()`. Rejected because the user description says "When a song ends, the next song to play should be calculated, skipping songs marked as such" — the flag is about queue progression, not just auto-advance. When the user clicks next, they also expect to land on the next playable song.

### 4. Auto-clear skip flags

**Rationale**: The skip flag is one-time and should be cleared in two situations:

1. **Skipped-past songs**: When `goForward()` advances past a flagged song, the function collects the IDs of all songs it skipped over and returns them. The caller clears those flags via the batch API using the `clearSkipNextPlayback` callback from `useQueueMutations` (which provides optimistic updates with rollback on error). This ensures the flag is consumed when a song is bypassed in the queue progression. This includes the case where all remaining songs are flagged — even though no next playable song is found, the flags are consumed because the songs were logically skipped past.

2. **Current song on playback start**: Unlike `StopAfterPlayback` (which clears when the song ends and triggers a pause), `SkipNextPlayback` clears when playback **starts** on the flagged song. If a song with the skip flag is played anyway (e.g., user clicks on it directly), the flag is cleared immediately so it doesn't persist. This ensures the flag is always consumed — either the song is skipped past (cleared by goForward) or the song plays (cleared on playback start).

**Alternative considered**: Clear flags server-side in the `SetQueueCurrentSong` endpoint. Rejected because that endpoint doesn't know about skip flags and mixing concerns would complicate the API. Client-side clearing is simpler and follows the same pattern as `StopAfterPlayback` flag clearing.

### 5. Visual indicator using `IconPlayerSkipForward`

**Rationale**: The user specifically requested `player-skip-forward` icon with gray (inactive) and yellow/orange (active) colors. This follows the same placement pattern as the `IconPlayerStop` used for `StopAfterPlayback` — in `SongTitle` component and collection action icon.

### 6. Interaction with `StopAfterPlayback`

If a song has both `StopAfterPlayback` and `SkipNextPlayback` flags, `StopAfterPlayback` takes precedence (pause instead of skip). The skip flag is not consumed in this case because the song never "finishes and advances past" — it pauses. The skip flag remains until the user manually advances or the stop flag is cleared.

### 7. Controller endpoints should use proper HTTP status codes and delegate to services

**Rationale**: The current implementation throws raw `Exception` resulting in 500 errors. The controller should return 404 NotFound when a playlist/song is not found and 403 Forbidden when the user doesn't own the playlist. Additionally, per project conventions (AGENTS.md: "Controllers are thin"), the business logic (ownership check, entity update, save) should be extracted into a service following the `<Resource><Operation>Service` pattern.

### 8. Notify user when playback stops because all remaining songs are flagged

**Rationale**: When `goForward()` finds no playable song (all remaining songs have `skipNextPlayback=true`), playback stops silently. This is confusing compared to the `stopAfterPlayback` behavior which shows a notification. A similar notification should inform the user that playback ended because all remaining songs were flagged to skip. The skip flags are cleared even when all remaining songs are flagged, because the songs were logically skipped past.

### 9. Mutual exclusivity between `SkipNextPlayback` and `StopAfterPlayback`

**Rationale**: A song cannot meaningfully both "stop playback" and "skip to the next song" at the same time. The two flags represent conflicting intents — one says "pause here", the other says "skip past me". Setting one to `true` should automatically clear the other. However, setting a flag to `false` should NOT affect the other flag, because the user is only removing one behavior, not expressing intent about the other.

**Implementation details**:
- In `PlaylistSongSkipService.SetSkipAfterPlayback`: when `skipNextPlayback=true`, also set `stopAfterPlayback=false` on the same `PlaylistSong` in the same transaction
- In `PlaylistSongSkipService.BatchSetSkipAfterPlayback`: when `skipNextPlayback=true`, also set `stopAfterPlayback=false` on all matching playlist songs in the same transaction
- In the existing stop-after-playback endpoints (or their future service): when `stopAfterPlayback=true`, also set `skipNextPlayback=false`
- On the frontend: the `toggleSkipNextPlayback` callback should also optimistically update `stopAfterPlayback` to `false` when setting `skipNextPlayback=true`. Similarly, `toggleStopAfterPlayback` should optimistically update `skipNextPlayback` to `false` when setting `stopAfterPlayback=true`
- The collection action for "Stop After This Song" and "Skip This Song" will naturally reflect mutual exclusivity through the server response + optimistic updates

**Alternative considered**: Allow both flags to coexist and handle precedence at runtime only. Rejected because it creates confusing UX — the user sees both icons but only one behavior activates. Mutual exclusivity prevents this confusion at the source.

### 10. Validate batch endpoint inputs

**Rationale**: The batch endpoint currently silently ignores invalid inputs (empty `songIds`, song IDs not in the playlist). This can lead to confusing behavior where the caller believes their request succeeded but some songs were not updated.

**Implementation details**:
- In `PlaylistSongSkipService.BatchSetSkipAfterPlayback`: throw `ArgumentException` if `songIds` is empty — there is no valid reason to call the batch endpoint with no song IDs
- In `PlaylistSongSkipService.BatchSetSkipAfterPlayback`: after fetching `playlistSongs`, compare the returned song IDs against the requested `songIds`. If any requested IDs are not found in the playlist, throw `ArgumentException` with a message indicating which IDs are invalid
- The controller catches `ArgumentException` and returns **400 Bad Request** with the error message

### 11. Rename `SkipAfterPlayback` to `SkipNextPlayback`

**Rationale**: The flag name `SkipAfterPlayback` is ambiguous — it could mean "skip after playback happens" (like stop-after). The name `SkipNextPlayback` more clearly indicates the one-time nature: this song will be skipped on the next playback pass, and the flag is then consumed. This applies to all layers: entity property, database column (`skip_next_playback`), DTO field, API endpoint paths, TypeScript types, and all frontend code.

**Scope of rename**:
- **Entity**: `PlaylistSong.SkipAfterPlayback` → `PlaylistSong.SkipNextPlayback`
- **Migration**: new migration to rename column `skip_after_playback` → `skip_next_playback`
- **DTOs**: `SetSkipAfterPlaybackRequest` → `SetSkipNextPlaybackRequest`, `BatchSetSkipAfterPlaybackRequest` → `BatchSetSkipNextPlaybackRequest`, `GetPlaylistSongItem.SkipAfterPlayback` → `GetPlaylistSongItem.SkipNextPlayback`
- **Service**: `IPlaylistSongSkipService` → `IPlaylistSongSkipService` (name stays the same), method params renamed
- **Controller**: endpoint paths `/skip-after-playback` → `/skip-next-playback`, route names updated
- **TypeScript models**: `skipAfterPlayback` → `skipNextPlayback`, request type names updated
- **Frontend code**: `use-queue.ts` callbacks (`toggleSkipAfterPlayback`, `clearSkipAfterPlayback`), `use-player-navigation.ts` references, `queue-utils.ts` default, `song-title.tsx` prop, `useSongsSchema.tsx` references, `orval.config.cjs` mutation names
- **Tests**: all test file references

## Risks / Trade-offs

- **[Multiple skips in a row]** → If many consecutive songs are flagged, `goForward()` must iterate through them all to find the next playable song. Mitigation: use index-based iteration on the already-sorted queue array (O(n) scan is fine for typical queue sizes < 100 songs).
- **[All remaining songs flagged]** → `goForward()` finds no playable song. Mitigation: playback ends with a user notification (same as reaching end of queue but with explanation). The skip flags are cleared because the songs were logically skipped past, even though no next song was found.
- **[Race condition on flag clearing]** → Multiple skip-flag clears could race. Mitigation: use optimistic update with rollback, same as `StopAfterPlayback` flag clearing. The `onSettled` invalidation ensures eventual consistency.
- **[Flag not cleared on error]** → If the batch clear API call fails, the skip flag remains in the database but the client rolled back to the previous state. Mitigation: the `onSettled` invalidation will re-fetch and the flag will reappear; the user can manually clear it.
- **[Rename is a breaking change]** → Renaming `SkipAfterPlayback` to `SkipNextPlayback` requires a database migration, API contract change, and frontend update. Mitigation: do the rename in a single coordinated change across all layers, and update the Orval-generated code in one pass.
