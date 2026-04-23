## 1. Backend API Enhancements

- [x] 1.1 Extend `AutocompleteAlbumItem` DTO with `artistId`, `artistName`, and `coverId` fields
- [x] 1.2 Extend `AutocompleteArtistItem` DTO with `coverId`, `albumCount`, and `songCount` fields
- [x] 1.3 Update `AutocompleteAlbums` endpoint to include artist ID, artist name, and cover ID in response
- [x] 1.4 Update `AutocompleteArtists` endpoint to include cover ID, album count, and song count in response
- [x] 1.5 Add database query optimizations for artist counts (consider caching or denormalization if needed)

## 2. Frontend Type Updates

- [x] 2.1 Run Orval to regenerate API client with new DTO fields
- [x] 2.2 Update `AutocompleteItem` interface in `autocomplete-field.tsx` to include `artistId`, `artistName`, `coverId`
- [x] 2.3 Update `TagsAutocompleteItem` interface in `tags-autocomplete-field.tsx` to include `coverId`, `albumCount`, `songCount`

## 3. Album Autocomplete Enhancement

- [x] 3.1 Update `searchAlbums` callback in `song-editor-context-modal.tsx` to map new API fields (artistId, artistName, coverId)
- [x] 3.2 Enhance `renderOption` in `autocomplete-field.tsx` to display artist name as subtitle
- [x] 3.3 Ensure placeholder icon displays when album has no cover
- [x] 3.4 Auto-populate Album Artist field when album with artist is selected

## 4. Artist Autocomplete Enhancement

- [x] 4.1 Add `renderOption` support to `tags-autocomplete-field.tsx` for custom option rendering
- [x] 4.2 Implement artist option display with cover artwork, album count, and song count
- [x] 4.3 Ensure placeholder icon displays when artist has no cover
- [x] 4.4 Update `searchArtists` callback in `song-editor-context-modal.tsx` to map new API fields

## 5. Album Artist Mismatch Warning

- [x] 5.1 Add mismatch detection logic in `song-editor-context-modal.tsx` (compare Album Artist field against form.artists)
- [x] 5.2 Implement warning message display on Album Artist field using `error` prop
- [x] 5.3 Style warning with error appearance (standard error red)
- [x] 5.4 Handle comparison logic: by ID for existing artists (id > 0), by name for new artists (id <= 0)

## 6. Testing and Verification

- [x] 6.1 Build and verify no TypeScript errors
- [x] 6.2 Test album autocomplete shows artist name and cover for duplicate-named albums
- [x] 6.3 Test artist autocomplete shows counts and cover for duplicate-named artists
- [x] 6.4 Test album artist mismatch warning appears and disappears correctly
- [x] 6.5 Test edge cases: null artist, null cover, zero counts

## 7. Bug Fix: Album Artist Field

- [x] 7.1 Fix Album Artist field to search artists instead of albums
- [x] 7.2 Add artwork display to Album Artist autocomplete
- [x] 7.3 Disable Album Artist field when album already exists (id > 0)
