## Context

The edit songs modal uses autocomplete dropdowns for selecting albums and artists. A recent fix allows items with duplicate names but different IDs to appear in dropdowns. However, users cannot distinguish between same-named items visually, leading to potential incorrect selections.

Current state:
- `AutocompleteAlbums` endpoint returns: `id`, `name`
- `AutocompleteArtists` endpoint returns: `id`, `name`
- `autocomplete-field.tsx` has `renderOption` support for albums with artwork (already used in some places)
- `tags-autocomplete-field.tsx` lacks custom option rendering

## Goals / Non-Goals

**Goals:**
- Enable users to visually distinguish same-named albums (by artist + cover)
- Enable users to visually distinguish same-named artists (by counts + cover)
- Warn users when selecting an album whose artist doesn't match the song's artists
- Maintain backward compatibility with existing API consumers

**Non-Goals:**
- Genre autocomplete enhancements (genres are typically unique by name)
- Autocomplete for other entities (playlists, devices)
- Batch/mass edit warning indicators
- Auto-correcting mismatches (warning only, user decides)

## Decisions

### 1. Backend: Extend Autocomplete DTOs

**Decision**: Add optional fields to existing DTOs rather than creating new endpoints.

**Rationale**: 
- Simpler migration path
- Existing consumers unaffected (additive changes)
- Single source of truth for autocomplete data

**Changes**:
```
AutocompleteAlbumItem:
  + artistId: long | null     (ID of album's artist, for mismatch validation)
  + artistName: string | null  (artist of the album, for display)
  + coverId: number | null     (for artwork display)

AutocompleteArtistItem:
  + coverId: number | null     (artist's image/artwork)
  + albumCount: int            (number of albums by this artist)
  + songCount: int             (number of songs by this artist)
```

### 2. Frontend: Custom Option Rendering

**Decision**: Use Mantine's `renderOption` prop for custom dropdown content.

**Rationale**:
- Already implemented for albums in `autocomplete-field.tsx` with `showArtwork` prop
- Native Mantine pattern, no custom components needed
- Consistent with existing artwork display patterns

**Implementation**:
- `autocomplete-field.tsx`: Enhance existing `renderOption` to show artist name subtitle
- `tags-autocomplete-field.tsx`: Add new `renderOption` prop support with artwork and counts

### 3. Album Selection Auto-populates Album Artist

**Decision**: When an existing album with an artist is selected, automatically populate the Album Artist field.

**Rationale**:
- Reduces manual data entry
- Ensures consistency between album and album artist
- Most users want the album artist to match the album's artist

**Logic**:
- When album is selected with `artistId` and `artistName`:
  - Set `form.albumArtist` to `{id: artistId, name: artistName}`
- When album is selected without artist info (null, or new album with id <= 0):
  - Do not change `form.albumArtist` (keep existing value)
- Album artist takes precedence over manually entered value when album changes

**Implementation**:
```tsx
onChange={(item) => {
    if (item && typeof item !== 'string') {
        const albumUpdate = {...};
        const albumArtistUpdate = item.artistId && item.artistName
            ? {id: item.artistId, name: item.artistName}
            : undefined; // undefined = don't change
        handleFormChange({
            album: albumUpdate,
            ...(albumArtistUpdate !== undefined && {albumArtist: albumArtistUpdate})
        });
    }
}}
```

### 4. Album Artist Mismatch Warning

**Decision**: Add a warning message on the Album Artist field using the `error` prop when mismatch detected.

**Rationale**:
- Non-blocking (warning, not error) - user may intentionally have different album artist
- Simple to implement with reactive state
- Matches Mantine's `Autocomplete` error pattern
- The Album Artist field is still indirectly editable by changing the album selection

**Logic**:
- Check the Album Artist field value against `form.artists`
- Comparison method depends on whether IDs are positive or negative:
  - `albumArtist.id > 0`: Match by ID with song artists that also have `id > 0`
  - `albumArtist.id <= 0`: Match by name with song artists that also have `id <= 0`
- Show warning on Album Artist field: "Not in the song's artists list"
- Warning updates reactively when Album Artist or song artists change

**Comparison Table**:
| Album Artist ID | Song Artist ID | Match Condition                   |
| --------------- | -------------- | --------------------------------- |
| `> 0`           | Same `> 0` ID  | Match ✓                           |
| `> 0`           | Different ID   | Mismatch ✗                        |
| `> 0`           | `<= 0`         | Mismatch ✗ (even if name matches) |
| `<= 0`          | `<= 0` same name| Match ✓                           |
| `<= 0`          | `<= 0` diff name| Mismatch ✗                        |
| `<= 0`          | `> 0`          | Mismatch ✗ (even if name matches) |

### 5. Artist Cover Artwork Source

**Decision**: Use artist's most recent album cover or a placeholder icon.

**Rationale**:
- Artists typically don't have dedicated profile images in music libraries
- Album cover provides visual context for the artist
- Consistent with how artist artwork appears in song lists

**Alternatives considered**:
- No artwork (text-only) - less visual distinction
- Dedicated artist images - would require schema changes

## Risks / Trade-offs

**Risk: Performance impact from count queries**
→ Mitigation: Cache artist counts in database (denormalize `album_count` and `song_count` on Artist entity), or compute on-demand with a fast count query

**Risk: Album artist null for compilations**
→ Mitigation: Handle null `artistName` gracefully in UI (show "Various Artists" or omit subtitle)

**Risk: Warning feels intrusive for intentional mismatches**
→ Mitigation: Use subtle styling (dimmed color, not error red) - it's informational, not blocking
