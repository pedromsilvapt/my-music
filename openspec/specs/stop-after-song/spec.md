## ADDED Requirements

### Requirement: Store stop-after flag in database
The system SHALL persist the `StopAfterPlayback` flag in the database as part of the playlist_songs table.

#### Scenario: Database schema includes stop-after column
- **WHEN** the database migration is applied
- **THEN** the `playlist_songs` table SHALL have a `stop_after_playback` boolean column
- **AND** the column SHALL default to `false`

#### Scenario: Flag persists across sessions
- **WHEN** a user sets the `StopAfterPlayback` flag on a playlist song
- **AND** the user refreshes the page or closes and reopens the application
- **THEN** the flag SHALL still be set on that playlist song

### Requirement: API for managing stop-after flag
The system SHALL provide API endpoints to get and set the `StopAfterPlayback` flag on playlist songs.

#### Scenario: Update flag via API
- **WHEN** client calls the update playlist song endpoint with `stopAfterPlayback` field
- **THEN** the server SHALL persist the flag value to the database
- **AND** the response SHALL include the updated playlist song data

#### Scenario: Read flag in playlist data
- **WHEN** client fetches playlist data
- **THEN** the response SHALL include the `StopAfterPlayback` field for each song in the playlist

#### Scenario: Batch update flag via API
- **WHEN** client calls the batch update endpoint with multiple song IDs and `stopAfterPlayback` field
- **THEN** the server SHALL update the flag for all specified songs in a single transaction
- **AND** the response SHALL include the updated playlist data

### Requirement: Efficient backend implementation
The system SHALL implement API endpoints efficiently to minimize database queries and network round-trips.

#### Scenario: Single query for flag update
- **WHEN** the server processes a flag update request
- **THEN** the server SHALL use a single database query to locate and update the playlist song
- **AND** ownership verification SHALL be performed within the same query

### Requirement: Collection actions schema integration
The system SHALL expose the `StopAfterPlayback` state through the collection actions schema for selected items.

#### Scenario: Action renders based on selected items
- **WHEN** the collection actions function receives selected items
- **THEN** the selected items data SHALL include the `StopAfterPlayback` property for each song
- **AND** the "Stop after this song" action SHALL render conditionally based on this data

#### Scenario: Toggle behavior with checkmark
- **WHEN** all selected songs have `StopAfterPlayback=true`
- **THEN** the action SHALL display with a checkmark indicator
- **AND** clicking the action SHALL clear the flag from all selected songs

#### Scenario: Mixed selection state
- **WHEN** some selected songs have `StopAfterPlayback=true` and others have `false`
- **THEN** the action SHALL display without a checkmark or with a partial indicator
- **AND** clicking the action SHALL set the flag on all selected songs

### Requirement: Mark song with stop-after flag
The system SHALL allow users to mark any song in a playlist with a `StopAfterPlayback` flag through the collection actions menu.

#### Scenario: User marks a song to stop after playback
- **WHEN** user selects a song in the now playing queue
- **AND** user opens the collection actions menu
- **AND** user selects the "Stop after this song" option
- **THEN** the song SHALL be marked with the `StopAfterPlayback` flag in the database
- **AND** a visual indicator SHALL appear on the song in the queue

#### Scenario: User unmarks a previously flagged song
- **WHEN** user selects a song that already has the `StopAfterPlayback` flag set
- **AND** user selects the "Stop after this song" option (which acts as a toggle)
- **THEN** the `StopAfterPlayback` flag SHALL be removed from the song in the database
- **AND** the visual indicator SHALL disappear

### Requirement: Visual indicator for flagged songs
The system SHALL provide a clear visual indicator on songs marked with the `StopAfterPlayback` flag in the now playing songs collection.

#### Scenario: Flagged song displays indicator
- **WHEN** a song in the now playing queue has the `StopAfterPlayback` flag set
- **THEN** a stop icon or badge SHALL be displayed alongside the song information
- **AND** the indicator SHALL be visible in all queue views (collapsed and expanded)

#### Scenario: Indicator updates on flag change
- **WHEN** the `StopAfterPlayback` flag is added or removed from a song
- **THEN** the visual indicator SHALL immediately appear or disappear without requiring a page refresh

### Requirement: Pause playback after flagged song
The system SHALL automatically pause playback when a song marked with `StopAfterPlayback` finishes playing, instead of advancing to the next song. When advancing past songs with `SkipNextPlayback`, those songs SHALL be skipped over and their flags cleared.

#### Scenario: Flagged song ends triggers pause
- **WHEN** the currently playing song reaches its end
- **AND** the song has the `StopAfterPlayback` flag set
- **THEN** playback SHALL pause
- **AND** the `StopAfterPlayback` flag SHALL be automatically cleared from the song via API call
- **AND** playback SHALL NOT advance to the next song in the queue

#### Scenario: Flag cleared after triggering
- **WHEN** playback pauses due to a `StopAfterPlayback` flag
- **THEN** the flag SHALL be removed from the song in the database
- **AND** the visual indicator SHALL disappear
- **AND** the client MAY optimistically clear the flag before server confirmation

#### Scenario: User manually skips flagged song
- **WHEN** user manually skips to the next song while a flagged song is playing
- **THEN** playback SHALL advance to the next non-skipped song (skipping over any `SkipNextPlayback` songs)
- **AND** the `SkipNextPlayback` flags on skipped-past songs SHALL be cleared
- **AND** the `StopAfterPlayback` flag SHALL remain on the skipped song (not cleared)

#### Scenario: Flag on last song in queue
- **WHEN** the last song in the queue has the `StopAfterPlayback` flag set
- **AND** the song finishes playing
- **THEN** playback SHALL pause (same behavior as middle of queue)
- **AND** the flag SHALL be cleared

#### Scenario: Setting StopAfterPlayback=true clears SkipNextPlayback
- **WHEN** a user sets `StopAfterPlayback=true` on a song (via single or batch API, or collection action)
- **AND** the song currently has `SkipNextPlayback=true`
- **THEN** the server SHALL set `SkipNextPlayback=false` on that song in the same transaction
- **AND** the response SHALL reflect both changes

#### Scenario: Setting StopAfterPlayback=false does not affect SkipNextPlayback
- **WHEN** a user sets `StopAfterPlayback=false` on a song
- **THEN** the `SkipNextPlayback` flag SHALL remain unchanged

### Requirement: Queue operations preserve flags appropriately
The system SHALL handle queue modifications while preserving stop-after flags appropriately.

#### Scenario: Remove flagged song from playlist
- **WHEN** a song marked with `StopAfterPlayback` is removed from the playlist
- **THEN** the flag SHALL be removed along with the song via database cascade or explicit cleanup

#### Scenario: Reorder queue with flagged songs
- **WHEN** the queue is reordered (e.g., via drag-and-drop)
- **THEN** the `StopAfterPlayback` flags SHALL remain associated with their respective playlist entries

#### Scenario: Clear queue removes all flags
- **WHEN** the entire queue is cleared
- **THEN** the playlist is either deleted or emptied, removing all `StopAfterPlayback` flags
