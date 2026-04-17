## ADDED Requirements

### Requirement: Lyrics Textarea auto-grows with content
The lyrics Textarea in the song editor modal SHALL grow in height to fit its content, up to a maximum row count, beyond which the Textarea SHALL scroll internally.

#### Scenario: Short lyrics fit within minimum rows
- **WHEN** a song has lyrics that fit within 3 rows
- **THEN** the lyrics Textarea displays at its minimum height (3 rows) with no internal scrollbar

#### Scenario: Long lyrics expand the Textarea
- **WHEN** a song has lyrics that exceed 3 rows but are within 12 rows
- **THEN** the lyrics Textarea height grows to fit the content without an internal scrollbar

#### Scenario: Very long lyrics capped at maximum height
- **WHEN** a song has lyrics that exceed 12 rows
- **THEN** the lyrics Textarea height caps at 12 rows and displays an internal scrollbar for the overflow content

### Requirement: Autosize applies to both old and new lyrics Textareas
Both the editable lyrics Textarea and the read-only "Lyrics (old)" Textarea (shown during metadata diff) SHALL support auto-growing with the same `maxRows` value.

#### Scenario: Diff mode with long old and new lyrics
- **WHEN** the song editor is in metadata diff mode and both old and new lyrics exceed 3 rows
- **THEN** both Textareas grow independently to fit their content, capped at 12 rows each

#### Scenario: Normal mode with long lyrics
- **WHEN** the song editor is in normal mode (no metadata diff) and lyrics exceed 3 rows
- **THEN** the single editable lyrics Textarea grows to fit content, capped at 12 rows

### Requirement: Diff mode lyrics Textareas share the same minimum height
In metadata diff mode, when both the "Lyrics (old)" and "Lyrics (new)" Textareas are displayed side-by-side, they SHALL share the same `minRows` value derived from whichever Textarea has more content, ensuring consistent heights.

#### Scenario: Old lyrics empty, new lyrics have content
- **WHEN** metadata diff mode is active and the old lyrics are empty but the new lyrics have 8 lines
- **THEN** both Textareas display with `minRows=8`, so the empty old Textarea matches the height of the new Textarea

#### Scenario: Both lyrics have similar content
- **WHEN** metadata diff mode is active and both old and new lyrics have 5 lines each
- **THEN** both Textareas display with `minRows=5`

#### Scenario: Content exceeds max rows
- **WHEN** metadata diff mode is active and one Textarea has 15 lines of content
- **THEN** both Textareas display with `minRows=12` (capped at maxRows), and the taller Textarea scrolls internally

#### Scenario: Short content, no diff mode
- **WHEN** normal mode (no metadata diff) and lyrics fit within 3 rows
- **THEN** the lyrics Textarea displays at `minRows=3`