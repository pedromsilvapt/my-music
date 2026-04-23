### Requirement: Album selection auto-populates Album Artist

The song editor SHALL automatically populate the Album Artist field when an existing album with an artist is selected.

#### Scenario: User selects existing album with artist
- **WHEN** user selects an album from the autocomplete dropdown
- **AND** the album has an associated artist (artistId > 0)
- **THEN** the Album Artist field is automatically set to the album's artist

#### Scenario: User selects existing album without artist
- **WHEN** user selects an album from the autocomplete dropdown
- **AND** the album has no associated artist (compilation)
- **THEN** the Album Artist field remains unchanged

#### Scenario: User types a new album name
- **WHEN** user types a new album name (not in database, id <= 0)
- **THEN** the Album Artist field remains unchanged
- **AND** the user can manually set the Album Artist

#### Scenario: User changes album after manually setting Album Artist
- **WHEN** user has manually set the Album Artist
- **AND** user selects a different existing album with an artist
- **THEN** the Album Artist field is updated to the new album's artist (album artist takes precedence)

#### Scenario: User clears album field
- **WHEN** user clears the album field
- **THEN** the Album Artist field remains unchanged

### Requirement: Album autocomplete shows artist and cover

The album autocomplete dropdown SHALL display the album's cover artwork and artist name for each option, enabling users to distinguish between albums with the same name by different artists.

#### Scenario: User searches for album with duplicate name
- **WHEN** user types in the album autocomplete field
- **AND** multiple albums with the same name exist by different artists
- **THEN** each dropdown option displays the album cover, album name, and artist name
- **AND** options are visually distinct by their cover art and artist name

#### Scenario: Album has no cover artwork
- **WHEN** an album has no cover artwork
- **THEN** the dropdown option displays a placeholder icon in place of the cover

#### Scenario: Album has no artist (compilation)
- **WHEN** an album has no associated artist (e.g., various artists compilation)
- **THEN** the dropdown option omits the artist subtitle or displays "Various Artists"

### Requirement: Artist autocomplete shows counts and cover

The artist autocomplete dropdown SHALL display the artist's cover artwork, album count, and song count for each option, enabling users to distinguish between artists with the same name.

#### Scenario: User searches for artist with duplicate name
- **WHEN** user types in the artist autocomplete field (tags input)
- **AND** multiple artists with the same name exist
- **THEN** each dropdown option displays the artist cover, artist name, album count, and song count
- **AND** options are visually distinct by their cover art and counts

#### Scenario: Artist has no cover artwork
- **WHEN** an artist has no cover artwork (no albums with covers)
- **THEN** the dropdown option displays a placeholder icon in place of the cover

#### Scenario: Artist has zero albums or songs
- **WHEN** an artist has zero albums or songs
- **THEN** the count displays as "0 albums, 0 songs" or is hidden if both are zero

### Requirement: Album artist mismatch warning

The song editor SHALL display a warning message on the Album Artist field when the album artist is not among the song's artists.

#### Scenario: Album artist does not match song artists (by ID)
- **WHEN** the Album Artist field has an artist with ID > 0
- **AND** the song's artists list does not include an artist with the same positive ID
- **THEN** a warning message appears on the Album Artist field
- **AND** the warning text indicates the mismatch (e.g., "Not in the song's artists list")

#### Scenario: Album artist matches song artists (by ID)
- **WHEN** the Album Artist field has an artist with ID > 0
- **AND** the song's artists list includes an artist with the same ID (also > 0)
- **THEN** no warning message appears

#### Scenario: New album artist matches by name
- **WHEN** the Album Artist field has a new artist (ID <= 0)
- **AND** the song's artists list includes a new artist (ID <= 0) with the same name
- **THEN** no warning message appears

#### Scenario: New album artist does not match by name
- **WHEN** the Album Artist field has a new artist (ID <= 0)
- **AND** the song's artists list does not include a new artist with the same name
- **THEN** a warning message appears on the Album Artist field

#### Scenario: Album Artist field is empty
- **WHEN** the Album Artist field has no value
- **THEN** no warning message appears

#### Scenario: Song has no artists
- **WHEN** the Album Artist field has a value
- **AND** the song has no artists assigned
- **THEN** no warning message appears (user may add artists later)

#### Scenario: Album artist field disabled due to existing album
- **WHEN** the Album Artist field is disabled (album exists in database)
- **AND** there is a mismatch
- **THEN** the warning message still appears on the Album Artist field
- **AND** the user can change the album to update the album artist

#### Scenario: Warning styling
- **WHEN** a warning message appears on the Album Artist field
- **THEN** the warning uses the field's standard error styling (red color)

### Requirement: Autocomplete API returns distinguishing metadata

The autocomplete API endpoints SHALL return additional metadata fields to support visual distinction in the frontend.

#### Scenario: Album autocomplete API response
- **WHEN** client calls the album autocomplete endpoint
- **THEN** each album item includes `id`, `name`, `artistId`, `artistName`, and `coverId` fields

#### Scenario: Artist autocomplete API response
- **WHEN** client calls the artist autocomplete endpoint
- **THEN** each artist item includes `id`, `name`, `coverId`, `albumCount`, and `songCount` fields
