## MODIFIED Requirements

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
