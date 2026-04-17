## 1. Lyrics Textarea Autosize

- [x] 1.1 Add `autosize` and `maxRows={12}` props to the editable lyrics Textarea in `song-editor-context-modal.tsx` (the "Lyrics (new)" Textarea)
- [x] 1.2 Add `autosize` and `maxRows={12}` props to the read-only lyrics Textarea in `song-editor-context-modal.tsx` (the "Lyrics (old)" Textarea inside `<Input.Wrapper>`)

## 2. Shared minRows in Diff Mode

- [x] 2.1 Add `lyricsMinRows` memo that computes shared minRows from both old and new lyrics line counts, clamped between 3 and 12
- [x] 2.2 Replace `minRows={3}` with `minRows={lyricsMinRows}` on both lyrics Textareas
- [x] 2.3 Revert `align="stretch"` back to `align="flex-start"` on the lyrics Group (flexbox stretch does not work with react-textarea-autosize's inline height)

## 3. Verification

- [x] 3.1 Run `npm run lint` in MyMusic.Client to verify no linting errors
- [x] 3.2 Visually verify the Textarea grows with content, caps at 12 rows, and both Textareas share height in diff mode