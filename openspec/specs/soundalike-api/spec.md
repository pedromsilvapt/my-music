## ADDED Requirements

### Requirement: API returns soundalike groups with full song details

The system SHALL provide an API endpoint that returns soundalike groups detected by acoustic fingerprinting, including complete metadata for each song in the group.

#### Scenario: Fetch soundalike groups for user
- **WHEN** client requests GET /api/audits/soundalikes with valid authentication
- **THEN** system returns list of soundalike groups with SongIds, MatchScore, PairwiseScores, and Signature
- **AND** each group includes fully hydrated song objects with all metadata (title, artists, album, year, genres, lyrics, artwork, bitrate, size, duration, created at)

#### Scenario: Soundalike groups ordered by match score
- **WHEN** client fetches soundalike groups
- **THEN** groups are returned ordered by match score descending (highest confidence first)

#### Scenario: Each song includes artwork preview URL
- **WHEN** soundalike groups are returned
- **THEN** each song includes artwork preview URL if artwork exists
- **AND** URL points to existing thumbnail proxy endpoint for preview display

### Requirement: API resolves duplicate groups with merge and delete

The system SHALL provide an API endpoint that accepts resolution requests to merge metadata and delete secondary songs.

#### Scenario: Resolve single group with primary selection
- **WHEN** client posts to /api/audits/soundalikes/resolve with primary song ID and secondary song IDs for a single group
- **THEN** system merges metadata from secondaries to primary using null-coalescing strategy
- **AND** system marks SongDevice records for removal (sets SongId = null, Action = Remove) to allow sync to delete files from devices
- **AND** system deletes PlaylistSong records for secondary songs
- **AND** system deletes secondary Song entities
- **AND** system deletes the audit non-conformity record for that group
- **AND** all operations execute within single database transaction

#### Scenario: Resolve multiple groups in batch
- **WHEN** client posts resolution request with multiple groups
- **THEN** system processes all groups within single transaction
- **AND** returns count of successfully resolved groups

#### Scenario: Reject resolution without primary selection
- **WHEN** client posts resolution request without primary song ID
- **THEN** system returns 400 Bad Request error
- **AND** no songs are deleted or merged

#### Scenario: Merge fills missing primary metadata
- **WHEN** primary song has missing year, lyrics, or artwork
- **AND** secondary songs have those fields populated
- **THEN** system copies missing values from first secondary that has the field
- **AND** primary's existing values are never overwritten

#### Scenario: Merge combines unique artists and genres
- **WHEN** primary song has some artists/genres
- **AND** secondaries have additional artists/genres
- **THEN** system merges unique artists/genres from all secondaries into primary

### Requirement: API validates ownership and authorization

The system SHALL validate that the requesting user owns all songs involved in a resolution operation.

#### Scenario: Reject resolution for songs owned by different user
- **WHEN** client attempts to resolve songs not owned by authenticated user
- **THEN** system returns 403 Forbidden error
- **AND** no songs are deleted or merged

#### Scenario: Include only user's own soundalike groups
- **WHEN** client fetches soundalike groups
- **THEN** only groups where all songs are owned by authenticated user are returned
