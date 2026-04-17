## Context

The song editor modal in MyMusic.Client contains a lyrics Textarea with `minRows={3}` and no `autosize` behavior. This renders a fixed-height box (~60px) that requires internal scrolling for any lyrics beyond a few lines, creating a poor editing experience for a field that commonly contains hundreds of words.

The modal itself is wrapped in a `ScrollArea`, and the lyrics Textarea sits inside a `Group` with `align="flex-start"` alongside a toggle checkbox (in diff mode) and a read-only "old" Textarea. Both Textareas use `flex: 1` for horizontal space.

Mantine's `Textarea` natively supports `autosize` (grows with content) and `maxRows` (scrolling cap), which is the idiomatic solution. However, Mantine's `autosize` is implemented via `react-textarea-autosize`, which sets an explicit pixel height via `style.setProperty('height', ..., 'important')`. This means CSS flexbox `align="stretch"` cannot make the actual `<textarea>` element fill the taller wrapper — only the outer `Box` wrapper stretches, not the inner HTML element.

## Goals / Non-Goals

**Goals:**
- Lyrics Textarea grows automatically with content height
- Growth caps at a reasonable maximum to prevent the Textarea from dominating the modal
- Beyond the cap, content scrolls within the Textarea
- Behavior applies to both editable and read-only lyrics Textareas
- In diff mode, both Textareas have the same height when one has content and the other is empty

**Non-Goals:**
- Changing the layout or horizontal sizing of the lyrics section
- Adding a drag-to-resize handle
- Modifying any other Textarea fields in the editor modal
- Backend or API changes

## Decisions

1. **Use Mantine's `autosize` + `maxRows` props** — Mantine's Textarea natively supports auto-growing via the `autosize` boolean prop, with `maxRows` capping the height. No custom JS, CSS, or third-party library needed. This is the idiomatic Mantine approach.

2. **Set `maxRows={12}`** — At ~24px per row, 12 rows yields ~288px which is roughly 40% of a typical modal viewport height (~700px). This gives enough room to see substantial lyrics while leaving space for other fields. The parent `ScrollArea` handles any overflow beyond that for the modal overall.

3. **Compute shared `minRows` via `lyricsMinRows` memo** — Because `react-textarea-autosize` (used internally by Mantine) sets explicit pixel heights, CSS flexbox `align="stretch"` cannot sync the heights of side-by-side Textareas — only the outer wrappers stretch, not the actual `<textarea>` element inside. Instead, compute a shared `minRows` value derived from the line count of whichever Textarea has more content. When both are short, `minRows` stays at 3. When one has long lyrics, `minRows` grows to match (capped at 12). This ensures the shorter Textarea starts at the same minimum height as the taller one.

4. **Revert `align="stretch"` back to `align="flex-start"`** — The flexbox stretch approach was attempted but does not work with `react-textarea-autosize`'s inline height. The `lyricsMinRows` memo replaces the need for it.

5. **Apply to both old and new Textareas** — Consistency matters. When viewing metadata diffs side-by-side, both Textareas should behave the same way.

## Risks / Trade-offs

- **[Risk] Textarea height may push other fields below viewport]** → Mitigated by `maxRows={12}` cap and the parent `ScrollArea` on the modal body. The modal scrolls independently of the Textarea.
- **[Risk] Diff mode with two auto-growing Textareas could make the row very tall]** → Mitigated by `maxRows` cap on both. The `flex-start` alignment on the `Group` ensures independent heights don't cause layout issues.
- **[Risk] `lineBreaks.length` underestimates rows for wrapped lines]** — `split('\n').length` counts explicit line breaks, not visual wrap lines. However, `autosize` handles growth beyond `minRows` automatically, so wrapped lines will still grow the Textarea. The shared `minRows` is a minimum guarantee, not a ceiling.