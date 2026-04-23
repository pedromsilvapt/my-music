## Why

When autocomplete dropdowns show multiple items with the same name (e.g., two albums named "Greatest Hits" by different artists, or two artists named "John Smith"), users cannot distinguish which item to select. This leads to incorrect selections and data quality issues. The recent fix for duplicate name handling (allowing same-named items with different IDs) exposed this UX gap - users now see duplicates but can't tell them apart.

## What Changes

- **Album autocomplete**: Display artist name and cover artwork in dropdown options for visual distinction
- **Artist autocomplete**: Display album count, song count, and cover artwork in dropdown options for visual distinction  
- **Album Artist autocomplete**: Search for artists (not albums), display artwork and counts
- **Album selection auto-population**: When an existing album with an artist is selected, automatically populate the Album Artist field
- **Album Artist field**: Disabled when album exists in database (id > 0)
- **Album artist mismatch warning**: Show a warning on the Album Artist field when the album artist is not among the song's artists (comparison by ID for existing artists, by name for new artists)
- **Backend API enhancement**: Extend autocomplete endpoints to return additional metadata (artist name, cover ID, album/song counts)

## Capabilities

### New Capabilities

- `autocomplete-distinguishing-info`: Displays distinguishing information in autocomplete dropdowns (cover art, artist name, counts) and warns about album artist mismatches

### Modified Capabilities

<!-- No existing capability requirements are changing - this is a new UX enhancement -->

## Impact

- **Backend**: 
  - `AutocompleteAlbums` endpoint returns additional fields (artistId, artistName, coverId)
  - `AutocompleteArtists` endpoint returns additional fields (albumCount, songCount, coverId)
- **Frontend**:
  - `autocomplete-field.tsx`: Enhanced `renderOption` for albums with artwork and artist name
  - `tags-autocomplete-field.tsx`: Enhanced option rendering for artists with artwork and counts
  - `song-editor-context-modal.tsx`: Warning message for album artist mismatch
- **API Contracts**: `AutocompleteAlbumItem` and `AutocompleteArtistItem` DTOs extended
