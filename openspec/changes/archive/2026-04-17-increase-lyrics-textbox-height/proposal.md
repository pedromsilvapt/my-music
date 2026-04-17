## Why

The lyrics Textarea in the song editor modal has a fixed height of 3 rows (`minRows={3}`), making it difficult to read or edit longer lyrics. Users must scroll within a tiny fixed-height box, which is a poor editing experience. The Textarea should grow with the content up to a maximum height, then scroll — a standard pattern Mantine supports via `autosize` + `maxRows`.

Additionally, in diff mode (metadata comparison), when the old lyrics are empty and the new lyrics have content (or vice versa), the two side-by-side Textareas have mismatched heights. Both Textareas should share the same minimum height based on whichever has more content.

## What Changes

- Add `autosize` prop to both the editable and read-only lyrics Textareas in the song editor modal
- Add `maxRows` prop to cap the height and enable scrolling beyond that limit
- Compute a shared `minRows` value (`lyricsMinRows`) from both old and new lyrics content, so both Textareas always have the same minimum height in diff mode
- Apply to both the "Lyrics (old)" read-only Textarea and the "Lyrics (new)" editable Textarea in `song-editor-context-modal.tsx`

## Capabilities

### New Capabilities
- `lyrics-autosize-textarea`: Lyrics Textarea in the song editor modal grows with content up to a maximum height, then scrolls; in diff mode, both Textareas share the same minimum height

### Modified Capabilities
<!-- No existing specs are modified -->

## Impact

- **Affected code**: `MyMusic.Client/src/components/songs/song-editor-context-modal.tsx`
- **Dependencies**: `@mantine/core` Textarea component (already in use; `autosize` and `maxRows` are built-in props)
- **No API changes**: Frontend-only change