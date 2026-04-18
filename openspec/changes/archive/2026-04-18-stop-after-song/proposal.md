## Why

Users need a way to automatically stop playback after a specific song ends, rather than continuing to play through the entire queue. This is useful when listening to a specific track or section of the queue without having to manually pause at the right moment. It provides precise control over playback flow, especially in scenarios where users want to focus on a particular song or stop at a natural break point. The flag should persist across sessions so users don't lose their settings when refreshing the page.

## What Changes

- Add a `StopAfterPlayback` boolean column to the `playlist_songs` table in the database
- Add a "stop after this song" toggle option to the song actions menu, appearing based on the selected items' flags
- The action shows a checkmark when selected items have the flag set, allowing toggle behavior for both single and multiple selections
- Pause playback when a song marked with `StopAfterPlayback` flag finishes playing
- Automatically clear the `StopAfterPlayback` flag once it triggers via API call
- Visually distinguish songs marked with the stop flag in the now playing songs collection (e.g., with an icon or highlight)
- The collection actions schema will expose the `StopAfterPlayback` state in selected items, allowing conditional rendering
- Batch API endpoint for updating multiple songs' flags efficiently in a single request

## Capabilities

### New Capabilities

- `stop-after-song`: Core feature to mark songs in playlists to stop playback after they finish, including server-side persistence, UI controls, and playback integration

### Modified Capabilities

- `playlist-management`: Extend playlist song entity to support `StopAfterPlayback` metadata and API endpoints for updating this flag

## Impact

- **MyMusic.Common**: 
  - `PlaylistSong` entity needs new `StopAfterPlayback` property
  - Database migration required for new column
- **MyMusic.Server**: 
  - New API endpoints to get/set `StopAfterPlayback` flag (single and batch)
  - DTO updates to include the new field
- **MyMusic.Client**: 
  - Update collection actions schema to include `StopAfterPlayback` in selected items
  - Player context to check flag at song completion
  - UI components for stop indicators
  - API integration for persisting flag changes (using batch endpoint for multi-select)
- **Database**: New migration for `playlist_songs.stop_after_playback` column
