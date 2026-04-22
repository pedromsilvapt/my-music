## 1. Backend: Entity & Migration

- [x] 1.1 Add `SkipAfterPlayback` bool property to `PlaylistSong` entity (`MyMusic.Common/Entities/PlaylistSong.cs`)
- [x] 1.2 Create EF Core migration for `skip_after_playback` column on `playlist_songs` table (non-nullable, default `false`)
- [x] 1.3 Update `MusicDbContextModelSnapshot` (auto-updated by migration tool)

## 2. Backend: DTOs

- [x] 2.1 Create `SetSkipAfterPlaybackRequest` record DTO (`MyMusic.Server/DTO/Playlists/SetSkipAfterPlaybackRequest.cs`)
- [x] 2.2 Create `BatchSetSkipAfterPlaybackRequest` record DTO (`MyMusic.Server/DTO/Playlists/BatchSetSkipAfterPlaybackRequest.cs`)
- [x] 2.3 Add `SkipAfterPlayback` property to `GetPlaylistSongItem` DTO and update `FromEntity` mapping (`MyMusic.Server/DTO/Playlists/GetPlaylistResponse.cs`)

## 3. Backend: Controller Endpoints

- [x] 3.1 Add `SetSkipAfterPlayback` endpoint: `PUT /playlists/{id}/songs/{songId}/skip-after-playback` (`MyMusic.Server/Controllers/PlaylistsController.cs`)
- [x] 3.2 Add `BatchSetSkipAfterPlayback` endpoint: `PUT /playlists/songs/skip-after-playback/batch` (`MyMusic.Server/Controllers/PlaylistsController.cs`)

## 4. Backend: Tests

- [x] 4.1 Write controller tests for `SetSkipAfterPlayback` endpoint (set true, set false, song not in playlist, another user's playlist)
- [x] 4.2 Write controller tests for `BatchSetSkipAfterPlayback` endpoint (batch set, batch clear, another user's playlist, non-existent playlist, only affects specified songs)

## 5. Frontend: Orval & Type Generation

- [x] 5.1 Re-run Orval to generate TypeScript models and API client for new endpoints (`orval.config.cjs` — add `setSkipAfterPlayback` and `batchSetSkipAfterPlayback` to `onMutations`)
- [x] 5.2 Verify generated `SetSkipAfterPlaybackRequest`, `BatchSetSkipAfterPlaybackRequest`, and updated `GetPlaylistSongItem` types
- [x] 5.3 Update `queue-utils.ts` `toPlaylistSong` to include `skipAfterPlayback: false` default

## 6. Frontend: Queue Mutations Hook

- [x] 6.1 Add `toggleSkipAfterPlayback` callback to `use-queue.ts` (optimistic update + server mutation with rollback), following the `toggleStopAfterPlayback` pattern
- [x] 6.2 Add `clearSkipAfterPlayback` callback for auto-clearing flags on skipped-past songs
- [x] 6.3 Expose both callbacks from the `useQueueMutations` hook

## 7. Frontend: Player Navigation — Skip Logic

- [x] 7.1 Modify `goForward()` in `use-player-navigation.ts` to skip past songs with `skipAfterPlayback === true`, returning the next playable song and the list of skipped song IDs
- [x] 7.2 Update `handleFinish` in `player-timeline-container.tsx` to call `clearSkipAfterPlayback` for all songs skipped past after `goForward()` lands on the next playable song
- [x] 7.3 Handle edge case: all remaining songs flagged — playback stops, skip flags ARE consumed (cleared)
- [x] 7.4 Handle interaction with `StopAfterPlayback`: if current song has both flags, `StopAfterPlayback` takes precedence (pause), `SkipAfterPlayback` is NOT consumed

## 8. Frontend: Visual Indicator

- [x] 8.1 Add `skipAfterPlayback` prop to `SongTitle` component (`MyMusic.Client/src/components/common/fields/song-title.tsx`), render `IconPlayerSkipForward` with yellow/orange color when active
- [x] 8.2 Update `useSongsSchema.tsx` title column to pass `skipAfterPlayback` prop from `isPlaylistSong` check
- [x] 8.3 Update `useSongsSchema.tsx` list view to pass `skipAfterPlayback` prop

## 9. Frontend: Collection Action

- [x] 9.1 Add "Skip this song" collection action in `useSongsSchema.tsx` (now-playing section), rendering `IconPlayerSkipForward` with gray (inactive) or yellow/orange (active) color, toggle behavior matching `StopAfterPlayback` pattern
- [x] 9.2 Verify action calls `toggleSkipAfterPlayback` from `useQueueMutations`

## 10. Verification

- [x] 10.1 Run `dotnet build` and verify no compilation errors
- [x] 10.2 Run `dotnet test` and verify all backend tests pass
- [x] 10.3 Run frontend build and verify no TypeScript errors in manually-written files

## 11. Code Review Fixes

- [x] 11.1 Use index-based iteration in `goForward()` instead of `queue.find(s => s.order === nextOrder)` — the server returns songs sorted by `Order` in the DTO mapping, so the `queue` array is already sorted and should be iterated by index
- [x] 11.2 Clear `skipAfterPlayback` flag on the current song when playback starts (in `navigateToSong` or equivalent), since the flag is one-time and should be consumed whether the song is skipped past OR played
- [x] 11.3 Use `clearSkipAfterPlayback` from `useQueueMutations` in `goForward()` instead of directly calling the raw mutation — ensures optimistic updates with rollback on error and proper cache invalidation
- [x] 11.4 Remove dead code: unused `clearSkipAfterPlaybackRef` in `player-timeline-container.tsx`
- [x] 11.5 Consolidate `clearSkipAfterPlayback` into `toggleSkipAfterPlayback` in `use-queue.ts` — `clearSkipAfterPlayback(songIds, queueId)` is equivalent to `toggleSkipAfterPlayback(songIds, false, queueId)` with an empty-array guard
- [x] 11.6 Extract business logic from controller endpoints into service classes per project conventions (`Songs/SongSkipService` etc.) — controllers should be thin (parse input, delegate to service, return output)
- [x] 11.7 Replace raw `Exception` throws with proper HTTP status codes: **404 Not Found** for missing playlist/song, **403 Forbidden** for unauthorized access
- [x] 11.8 Add user notification when playback stops because all remaining songs in the queue have `skipAfterPlayback` set (currently stops silently)
- [x] 11.9 Re-run verification: `dotnet build`, `dotnet test`, frontend build

## 12. Rename SkipAfterPlayback → SkipNextPlayback

- [x] 12.1 Rename `PlaylistSong.SkipAfterPlayback` → `PlaylistSong.SkipNextPlayback` in `MyMusic.Common/Entities/PlaylistSong.cs`
- [x] 12.2 Create EF Core migration to rename column `skip_after_playback` → `skip_next_playback` on `playlist_songs` table
- [x] 12.3 Rename `SetSkipAfterPlaybackRequest` → `SetSkipNextPlaybackRequest` in `MyMusic.Server/DTO/Playlists/`, update `skipAfterPlayback` → `skipNextPlayback` field
- [x] 12.4 Rename `BatchSetSkipAfterPlaybackRequest` → `BatchSetSkipNextPlaybackRequest` in `MyMusic.Server/DTO/Playlists/`, update fields
- [x] 12.5 Rename `GetPlaylistSongItem.SkipAfterPlayback` → `GetPlaylistSongItem.SkipNextPlayback` in `MyMusic.Server/DTO/Playlists/GetPlaylistResponse.cs`, update `FromEntity` mapping parameter and `ps.SkipAfterPlayback` → `ps.SkipNextPlayback`
- [x] 12.6 Update controller route names and paths: `SetSkipAfterPlayback` → `SetSkipNextPlayback`, `/skip-after-playback` → `/skip-next-playback`, `BatchSetSkipAfterPlayback` → `BatchSetSkipNextPlayback`, `/skip-after-playback/batch` → `/skip-next-playback/batch` (`MyMusic.Server/Controllers/PlaylistsController.cs`)
- [x] 12.7 Update `IPlaylistSongSkipService` and `PlaylistSongSkipService`: rename method params `skipAfterPlayback` → `skipNextPlayback`, all internal references
- [x] 12.8 Update DI registration in `HostBuilderExtensions.cs` (service name unchanged but verify)
- [x] 12.9 Re-run Orval to regenerate TypeScript client/models with new endpoint names and field names
- [x] 12.10 Update `orval.config.cjs` mutation names: `setSkipAfterPlayback` → `setSkipNextPlayback`, `batchSetSkipAfterPlayback` → `batchSetSkipNextPlayback`
- [x] 12.11 Update `queue-utils.ts`: `skipAfterPlayback: false` → `skipNextPlayback: false` in `toPlaylistSong`
- [x] 12.12 Update `use-queue.ts`: rename `toggleSkipAfterPlayback` → `toggleSkipNextPlayback`, `clearSkipAfterPlayback` → `clearSkipNextPlayback`, all `skipAfterPlayback` → `skipNextPlayback` references, `useBatchSetSkipAfterPlayback` → `useBatchSetSkipNextPlayback`
- [x] 12.13 Update `use-player-navigation.ts`: rename `clearSkipAfterPlayback` → `clearSkipNextPlayback`, all `skipAfterPlayback` → `skipNextPlayback` references
- [x] 12.14 Update `song-title.tsx`: rename `skipAfterPlayback` prop → `skipNextPlayback`
- [x] 12.15 Update `useSongsSchema.tsx`: rename all `skipAfterPlayback` → `skipNextPlayback` references, `toggleSkipAfterPlayback` → `toggleSkipNextPlayback`
- [x] 12.16 Update `player-timeline-container.tsx`: any remaining `skipAfterPlayback` references → `skipNextPlayback`
- [x] 12.17 Update test files: rename all `SkipAfterPlayback` → `SkipNextPlayback` in `PlaylistsControllerSpecs.SkipAfterPlayback.cs` (consider renaming file to `PlaylistsControllerSpecs.SkipNextPlayback.cs`), update request DTO names and field names
- [x] 12.18 Run full verification: `dotnet build`, `dotnet test`, frontend build

## 13. Mutual Exclusivity: SkipNextPlayback ↔ StopAfterPlayback

- [x] 13.1 Update `PlaylistSongSkipService.SetSkipAfterPlayback`: when `skipNextPlayback=true`, also set `playlistSong.StopAfterPlayback = false` in the same `SaveChangesAsync` call
- [x] 13.2 Update `PlaylistSongSkipService.BatchSetSkipAfterPlayback`: when `skipNextPlayback=true`, also set `stopAfterPlayback=false` on all matching playlist songs in the same transaction
- [x] 13.3 Update the existing stop-after-playback endpoints (or their future service) to set `SkipNextPlayback=false` when `stopAfterPlayback=true` — applies to both single and batch endpoints in `PlaylistsController.cs`
- [x] 13.4 Add controller test: `SetSkipNextPlayback` with `true` clears `StopAfterPlayback` on the same song
- [x] 13.5 Add controller test: `SetSkipNextPlayback` with `false` does NOT affect `StopAfterPlayback`
- [x] 13.6 Add controller test: `BatchSetSkipNextPlayback` with `true` clears `StopAfterPlayback` on all specified songs
- [x] 13.7 Add controller test: `SetStopAfterPlayback` with `true` clears `SkipNextPlayback` on the same song (update existing stop-after tests or add new)
- [x] 13.8 Add controller test: `SetStopAfterPlayback` with `false` does NOT affect `SkipNextPlayback`
- [x] 13.9 Update frontend `toggleSkipNextPlayback` in `use-queue.ts`: when `skipNextPlayback=true`, also optimistically set `stopAfterPlayback=false` on the same songs in the queue cache
- [x] 13.10 Update frontend `toggleStopAfterPlayback` in `use-queue.ts`: when `stopAfterPlayback=true`, also optimistically set `skipNextPlayback=false` on the same songs in the queue cache
- [x] 13.11 Update collection action handlers in `useSongsSchema.tsx`: when "Skip This Song" is toggled on, the optimistic update should clear the stop icon; when "Stop After This Song" is toggled on, the optimistic update should clear the skip icon

## 14. Batch Endpoint Validation

- [x] 14.1 Add `ArgumentException` guard in `PlaylistSongSkipService.BatchSetSkipAfterPlayback` for empty `songIds` list — throw immediately before any DB queries
- [x] 14.2 Add validation in `PlaylistSongSkipService.BatchSetSkipAfterPlayback` for song IDs not in playlist: after fetching `playlistSongs`, compare against requested `songIds` and throw `ArgumentException` with the invalid IDs listed if any are missing
- [x] 14.3 Update `PlaylistsController` to catch `ArgumentException` and return **400 Bad Request** with the error message (add a catch block alongside the existing `KeyNotFoundException` and `UnauthorizedAccessException` catches, for both skip and stop batch endpoints)
- [x] 14.4 Add controller test: `BatchSetSkipNextPlayback` with empty `songIds` returns 400
- [x] 14.5 Add controller test: `BatchSetSkipNextPlayback` with song IDs not in the playlist returns 400
- [x] 14.6 Add controller test: `BatchSetSkipNextPlayback` with partially invalid song IDs (some in playlist, some not) returns 400

## 15. Verification

- [x] 15.1 Run `dotnet build` and verify no compilation errors
- [x] 15.2 Run `dotnet test` and verify all backend tests pass (including new mutual exclusivity and validation tests)
- [x] 15.3 Run frontend build and verify no TypeScript errors in manually-written files

## 16. Bug Fix: All-remaining-skipped handling in manual next button

### Context
When the user clicks the "next" button and all remaining songs have `skipNextPlayback=true`, the skip flags are cleared correctly but playback continues on the current song. The `goForward()` result is passed directly to `onClick` and discarded, so the `allRemainingSkipped` case is never handled.

The `handleFinish` path (automatic advancement when song ends) handles this correctly, but the manual next button path does not.

- [x] 16.1 Create a wrapper function in `player-controls-container.tsx` that calls `goForward()`, checks `result?.allRemainingSkipped`, and stops playback + shows notification (matching the `handleFinish` behavior)
- [x] 16.2 Pass the wrapper function to `playNext` prop instead of `goForward` directly
- [ ] 16.3 Manual test: queue with 2 songs, set skip flag on last song, play first song, click next — verify playback stops and notification appears
- [x] 16.4 Run frontend build and verify no TypeScript errors
