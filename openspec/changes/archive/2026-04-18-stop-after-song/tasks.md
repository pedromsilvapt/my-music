## 1. Type Definitions and State Management

- [x] 1.1 Add `StopAfterPlayback` property to PlaylistSong entity in MyMusic.Common
- [x] 1.2 Create database migration for new column in playlist_songs table
- [x] 1.3 Update relevant DTOs to include `StopAfterPlayback` field
- [x] 1.4 Update API endpoints to support setting/unsetting the flag
- [x] 1.5 Update playback store to sync stop-after state from server

## 2. Song Actions Menu Integration

- [x] 2.1 Locate the collection actions function/schema for now playing queue
- [x] 2.2 Update schema to include `StopAfterPlayback` in selected items data
- [x] 2.3 Add "Stop after this song" action that appears based on selected items' flags
- [x] 2.4 Show checkmark when all selected songs have the flag, partial for mixed state
- [x] 2.5 Connect action to API call for updating the flag in database

## 3. Visual Indicator Implementation

- [x] 3.1 Create or locate the song item component in now playing queue
- [x] 3.2 Add stop icon (IconPlayerStop from @tabler/icons-react) indicator for flagged songs
- [x] 3.3 Style indicator with Mantine theme colors (use `theme.colors.red` or similar)
- [x] 3.4 Ensure indicator is visible in both collapsed and expanded queue views
- [x] 3.5 Add tooltip to indicator explaining "Playback will stop after this song"

## 4. Playback Completion Integration

- [x] 4.1 Locate the song completion/onEnded handler in playback engine
- [x] 4.2 Add check for `StopAfterPlayback` flag before advancing to next song
- [x] 4.3 When flag is detected: pause playback instead of advancing
- [x] 4.4 When flag is detected: automatically clear the flag via API call
- [x] 4.5 Ensure manual skip (next/previous buttons) does NOT trigger flag behavior

## 5. Edge Cases and Polish

- [x] 5.1 Handle case where flagged song is removed from playlist (flag removed via cascade)
- [x] 5.2 Handle case where flagged song is the last song (pause at end)
- [x] 5.3 Add notification toast when playback stops due to flag (optional, see design risks)
- [x] 5.4 Test with duplicate songs in queue (each playlist entry has independent flag)
- [x] 5.5 Verify behavior when user toggles flag on currently playing song

## 6. Testing and Verification

- [x] 6.1 Test database migration applies correctly
- [x] 6.2 Test API endpoints for setting/unsetting flag
- [ ] 6.3 Manual test: Mark song and let it finish - verify playback pauses
- [ ] 6.4 Manual test: Unmark song - verify flag is removed and indicator disappears
- [ ] 6.5 Manual test: Multi-select songs - verify action shows correct state
- [ ] 6.6 Manual test: Reload page - verify flags persist from database
- [ ] 6.7 Manual test: Skip flagged song manually - verify flag remains and playback continues
- [x] 6.8 Verify no TypeScript errors in modified files
- [x] 6.9 Verify no regressions in existing playback functionality

## 7. Code Review Fixes

- [x] 7.1 Add batch API endpoint for updating multiple songs' stop-after flag at once (eliminates N+1 calls)
- [x] 7.2 Update frontend `toggleStopAfterPlayback` to use batch endpoint instead of loop
- [x] 7.3 Fix falsy `queueId` check in `player-timeline-container.tsx` (use `!= null` instead of truthy check)
- [ ] 7.4 Add `onSettled` callback with query invalidation to `toggleStopAfterPlayback`
- [x] 7.5 Optimize `SetStopAfterPlayback` endpoint to use single DB query with join
- [x] 7.6 Add error handling/notification when flag clear API call fails in `handleFinish`
- [x] 7.7 Create type guard function `isPlaylistSong()` instead of `as GetPlaylistSongItem` casting
- [x] 7.8 Update notification wording to match spec ("Stopped after this song")
- [x] 7.9 Add unit tests for `SetStopAfterPlayback` endpoint
- [x] 7.10 Add unit tests for frontend `toggleStopAfterPlayback` mutation logic
