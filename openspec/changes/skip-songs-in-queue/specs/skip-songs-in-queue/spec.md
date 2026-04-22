## ADDED Requirements

### Requirement: Store skip-next flag in database
The system SHALL persist the `SkipNextPlayback` flag in the database as part of the `playlist_songs` table.

#### Scenario: Database schema includes skip-next column
- **WHEN** the database migration is applied
- **THEN** the `playlist_songs` table SHALL have a `skip_next_playback` boolean column
- **AND** the column SHALL default to `false`

#### Scenario: Flag persists across sessions
- **WHEN** a user sets the `SkipNextPlayback` flag on a playlist song
- **AND** the user refreshes the page or closes and reopens the application
- **THEN** the flag SHALL still be set on that playlist song

### Requirement: API for managing skip-next flag
The system SHALL provide API endpoints to set and clear the `SkipNextPlayback` flag on playlist songs.

#### Scenario: Update flag via single-song API
- **WHEN** client calls `PUT /playlists/{playlistId}/songs/{songId}/skip-next-playback` with `skipNextPlayback` field
- **THEN** the server SHALL persist the flag value to the database
- **AND** the server SHALL verify ownership within the same query
- **AND** the response SHALL include the updated playlist data

#### Scenario: Batch update flag via API
- **WHEN** client calls `PUT /playlists/songs/skip-next-playback/batch` with multiple song IDs and `skipNextPlayback` field
- **THEN** the server SHALL update the flag for all specified songs in a single transaction
- **AND** only songs belonging to the specified playlist SHALL be updated
- **AND** the response SHALL include the updated playlist data

#### Scenario: Song not in playlist
- **WHEN** client calls the single-song endpoint for a song not in the specified playlist
- **THEN** the server SHALL return **404 Not Found**

#### Scenario: Another user's playlist
- **WHEN** client calls the endpoint for a playlist owned by another user
- **THEN** the server SHALL return **403 Forbidden**

#### Scenario: Read flag in playlist data
- **WHEN** client fetches playlist data
- **THEN** the response SHALL include the `SkipNextPlayback` field for each song in the playlist

#### Scenario: Batch with empty song IDs
- **WHEN** client calls the batch endpoint with an empty `songIds` list
- **THEN** the server SHALL return **400 Bad Request**

#### Scenario: Batch with song IDs not in playlist
- **WHEN** client calls the batch endpoint with `songIds` that include IDs not present in the specified playlist
- **THEN** the server SHALL return **400 Bad Request**
- **AND** the error response SHALL indicate which song IDs are invalid

### Requirement: Skip past flagged songs on queue advancement
The system SHALL skip over songs marked with `SkipNextPlayback` when advancing to the next song in the queue.

#### Scenario: Song ends with next song flagged as skip
- **WHEN** the currently playing song reaches its end
- **AND** the next song in the queue has the `SkipNextPlayback` flag set
- **THEN** playback SHALL advance to the next non-skipped song after it
- **AND** the `SkipNextPlayback` flag SHALL be automatically cleared on all songs that were skipped past

#### Scenario: Multiple consecutive skip-flagged songs
- **WHEN** the currently playing song reaches its end
- **AND** the next several consecutive songs all have the `SkipNextPlayback` flag set
- **THEN** playback SHALL advance to the first non-skipped song after them
- **AND** the `SkipNextPlayback` flag SHALL be cleared on all skipped-past songs

#### Scenario: All remaining songs flagged as skip (automatic advancement)
- **WHEN** the currently playing song reaches its end
- **AND** all subsequent songs in the queue have the `SkipNextPlayback` flag set
- **THEN** playback SHALL stop (end of queue behavior)
- **AND** the system SHALL notify the user that playback stopped because all remaining songs were flagged to skip
- **AND** the `SkipNextPlayback` flags SHALL be cleared on all those songs (the flags were consumed — the songs were skipped past even though no next song was found to play)

#### Scenario: All remaining songs flagged as skip (manual next button)
- **WHEN** the user clicks the next button to advance to the next song
- **AND** all subsequent songs in the queue have the `SkipNextPlayback` flag set
- **THEN** playback SHALL stop on the current song
- **AND** the system SHALL notify the user that playback stopped because all remaining songs were flagged to skip
- **AND** the `SkipNextPlayback` flags SHALL be cleared on all those songs

#### Scenario: No songs flagged as skip
- **WHEN** the currently playing song reaches its end
- **AND** no subsequent songs have the `SkipNextPlayback` flag set
- **THEN** playback SHALL advance to the next song normally

### Requirement: One-time skip behavior
The `SkipNextPlayback` flag SHALL be a one-time flag that is automatically cleared when the flag is consumed — either when a song is skipped past during queue advancement, or when playback starts on a flagged song.

#### Scenario: Flag cleared after song is skipped past
- **WHEN** playback advances past a song with the `SkipNextPlayback` flag
- **THEN** the flag SHALL be cleared on that song via the `clearSkipNextPlayback` callback with optimistic update and rollback on error
- **AND** the song SHALL play normally in subsequent queue cycles

#### Scenario: Flag cleared when playback starts on a flagged song
- **WHEN** a song with the `SkipNextPlayback` flag begins playing (e.g., user clicks on it directly)
- **THEN** the flag SHALL be cleared on that song immediately at playback start
- **AND** the song SHALL play normally in subsequent queue cycles

#### Scenario: Flag not cleared when manually navigating
- **WHEN** the user manually skips forward using the next button
- **AND** the next song calculation skips past a song with `SkipNextPlayback`
- **THEN** the flag SHALL be cleared on all skipped-past songs
- **AND** the behavior SHALL be the same as automatic advancement

#### Scenario: Flag not cleared when user navigates backward
- **WHEN** the user navigates backward to a previous song
- **THEN** the `SkipNextPlayback` flag on any songs SHALL remain unchanged

### Requirement: Visual indicator for skip-flagged songs
The system SHALL provide a clear visual indicator on songs marked with the `SkipNextPlayback` flag in the now playing queue.

#### Scenario: Flagged song displays skip indicator
- **WHEN** a song in the now playing queue has the `SkipNextPlayback` flag set
- **THEN** a `player-skip-forward` icon SHALL be displayed alongside the song information
- **AND** the icon SHALL use yellow/orange color for active (flagged) state
- **AND** the indicator SHALL be visible in all queue views (collapsed and expanded)

#### Scenario: Collection action icon renders based on selection
- **WHEN** the collection actions menu is opened for selected songs
- **THEN** the "Skip this song" action SHALL render a `player-skip-forward` icon
- **AND** if all selected songs have `SkipNextPlayback=true`, the icon SHALL use yellow/orange color
- **AND** if not all selected songs have the flag, the icon SHALL use gray color with reduced opacity

#### Scenario: Indicator updates on flag change
- **WHEN** the `SkipNextPlayback` flag is added or removed from a song
- **THEN** the visual indicator SHALL immediately appear or disappear without requiring a page refresh

### Requirement: Collection actions to toggle skip flag
The system SHALL expose the `SkipNextPlayback` state through the collection actions schema for selected items in the now playing queue.

#### Scenario: User marks a song to skip after playback
- **WHEN** user selects a song in the now playing queue
- **AND** user opens the collection actions menu
- **AND** user selects the "Skip this song" option
- **THEN** the song SHALL be marked with the `SkipNextPlayback` flag in the database
- **AND** the `StopAfterPlayback` flag on that song SHALL be cleared (set to `false`)
- **AND** a visual indicator SHALL appear on the song in the queue

#### Scenario: User unmarks a previously flagged song
- **WHEN** user selects a song that already has the `SkipNextPlayback` flag set
- **AND** user selects the "Skip this song" option (which acts as a toggle)
- **THEN** the `SkipNextPlayback` flag SHALL be removed from the song in the database
- **AND** the `StopAfterPlayback` flag SHALL NOT be changed (only clearing `SkipNextPlayback`, not touching the other flag)
- **AND** the visual indicator SHALL disappear

#### Scenario: Toggle behavior with mixed selection
- **WHEN** some selected songs have `SkipNextPlayback=true` and others have `false`
- **THEN** clicking the action SHALL set the flag on all selected songs
- **AND** the `StopAfterPlayback` flag SHALL be cleared on all selected songs

#### Scenario: Toggle behavior with all-flagged selection
- **WHEN** all selected songs have `SkipNextPlayback=true`
- **THEN** clicking the action SHALL clear the flag from all selected songs
- **AND** the `StopAfterPlayback` flag SHALL NOT be changed

### Requirement: Mutual exclusivity with StopAfterPlayback
The `SkipNextPlayback` and `StopAfterPlayback` flags SHALL be mutually exclusive when being set to `true`. Setting one flag to `true` SHALL automatically clear the other flag. Setting a flag to `false` SHALL NOT affect the other flag.

#### Scenario: Setting SkipNextPlayback=true clears StopAfterPlayback
- **WHEN** a user sets `SkipNextPlayback=true` on a song (via single or batch API, or collection action)
- **AND** the song currently has `StopAfterPlayback=true`
- **THEN** the server SHALL set `StopAfterPlayback=false` on that song in the same transaction
- **AND** the response SHALL reflect both changes

#### Scenario: Setting StopAfterPlayback=true clears SkipNextPlayback
- **WHEN** a user sets `StopAfterPlayback=true` on a song (via single or batch API, or collection action)
- **AND** the song currently has `SkipNextPlayback=true`
- **THEN** the server SHALL set `SkipNextPlayback=false` on that song in the same transaction
- **AND** the response SHALL reflect both changes

#### Scenario: Setting SkipNextPlayback=false does not affect StopAfterPlayback
- **WHEN** a user sets `SkipNextPlayback=false` on a song
- **THEN** the `StopAfterPlayback` flag SHALL remain unchanged

#### Scenario: Setting StopAfterPlayback=false does not affect SkipNextPlayback
- **WHEN** a user sets `StopAfterPlayback=false` on a song
- **THEN** the `SkipNextPlayback` flag SHALL remain unchanged

### Requirement: Interaction with StopAfterPlayback
The system SHALL handle songs that have both `StopAfterPlayback` and `SkipNextPlayback` flags set.

#### Scenario: Both flags set on a song
- **WHEN** a song has both `StopAfterPlayback` and `SkipNextPlayback` flags set
- **AND** the song finishes playing
- **THEN** the `StopAfterPlayback` behavior SHALL take precedence (playback pauses)
- **AND** the `SkipNextPlayback` flag SHALL NOT be cleared (the song was not skipped past)

#### Scenario: StopAfterPlayback cleared, SkipNextPlayback remains
- **WHEN** a song previously had both flags set
- **AND** the `StopAfterPlayback` flag was cleared (either manually or after triggering)
- **AND** the `SkipNextPlayback` flag is still set
- **THEN** the next time the song is played and finishes, the skip behavior SHALL apply

### Requirement: Queue operations preserve skip flags appropriately
The system SHALL handle queue modifications while preserving skip flags appropriately.

#### Scenario: Remove skip-flagged song from playlist
- **WHEN** a song marked with `SkipNextPlayback` is removed from the playlist
- **THEN** the flag SHALL be removed along with the song via database cascade or explicit cleanup

#### Scenario: Reorder queue with skip-flagged songs
- **WHEN** the queue is reordered
- **THEN** the `SkipNextPlayback` flags SHALL remain associated with their respective playlist entries

#### Scenario: Clear queue removes all skip flags
- **WHEN** the entire queue is cleared
- **THEN** the playlist is either deleted or emptied, removing all `SkipNextPlayback` flags

#### Scenario: New songs added to queue
- **WHEN** new songs are added to the queue
- **THEN** the `SkipNextPlayback` flag SHALL default to `false`
