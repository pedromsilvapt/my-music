## ADDED Requirements

### Requirement: Collection fields support equality filtering

The client-side filter evaluator SHALL support equality operators (`=`, `==`) on collection fields (artists, genres, devices). For equality, the system SHALL match if ANY element in the collection equals the filter value.

#### Scenario: Filter songs by artist name equality
- **WHEN** user applies filter `artist.name = "Taylor Swift"` on a song collection in client mode
- **THEN** all songs where at least one artist's name equals "Taylor Swift" SHALL be included in results

#### Scenario: Filter songs by genre name equality
- **WHEN** user applies filter `genre.name = "Rock"` on a song collection in client mode
- **THEN** all songs where at least one genre's name equals "Rock" SHALL be included in results

#### Scenario: Filter songs by device name equality
- **WHEN** user applies filter `device.name = "iPhone"` on a song collection in client mode
- **THEN** all songs where at least one device's name equals "iPhone" SHALL be included in results

### Requirement: Collection fields support negative equality filtering

The client-side filter evaluator SHALL support negative equality operators (`!=`, `<>`) on collection fields. For negative equality, the system SHALL match if NO element in the collection equals the filter value.

#### Scenario: Exclude songs by artist name
- **WHEN** user applies filter `artist.name != "Taylor Swift"` on a song collection in client mode
- **THEN** all songs where NO artist's name equals "Taylor Swift" SHALL be included in results

#### Scenario: Exclude songs by genre name
- **WHEN** user applies filter `genre.name != "Pop"` on a song collection in client mode
- **THEN** all songs where NO genre's name equals "Pop" SHALL be included in results

### Requirement: Collection fields support substring operators

The client-side filter evaluator SHALL support substring operators (`contains`, `~`, `startsWith`, `endsWith`) on collection fields. For these operators, the system SHALL match if ANY element satisfies the condition.

#### Scenario: Filter songs by artist name contains
- **WHEN** user applies filter `artist.name contains "Beatles"` on a song collection in client mode
- **THEN** all songs where at least one artist's name contains "Beatles" SHALL be included in results

#### Scenario: Filter songs by genre name starts with
- **WHEN** user applies filter `genre.name startsWith "Jazz"` on a song collection in client mode
- **THEN** all songs where at least one genre's name starts with "Jazz" SHALL be included in results

#### Scenario: Filter songs by device name ends with
- **WHEN** user applies filter `device.name endsWith "Pod"` on a song collection in client mode
- **THEN** all songs where at least one device's name ends with "Pod" SHALL be included in results

### Requirement: Non-collection fields remain unchanged

The client-side filter evaluator SHALL maintain existing behavior for non-collection fields. Fields like `title`, `album.name`, `year` SHALL continue to work with single-value semantics.

#### Scenario: Album name filter still works
- **WHEN** user applies filter `album.name = "Abbey Road"` on a song collection in client mode
- **THEN** all songs where the album's name equals "Abbey Road" SHALL be included in results

#### Scenario: Title contains filter still works
- **WHEN** user applies filter `title contains "Love"` on a song collection in client mode
- **THEN** all songs where the title contains "Love" SHALL be included in results

### Requirement: Empty collection handling

The client-side filter evaluator SHALL handle empty collections gracefully. For songs with no artists/genres/devices, the filter SHALL return false for positive operators and true for negative operators.

#### Scenario: Song with no genres filtered out
- **WHEN** user applies filter `genre.name = "Rock"` on a song that has no genres
- **THEN** the song SHALL NOT be included in results (no matching genre)

#### Scenario: Song with no devices passes negative filter
- **WHEN** user applies filter `device.name != "iPhone"` on a song that has no devices
- **THEN** the song SHALL be included in results (no conflicting device)
