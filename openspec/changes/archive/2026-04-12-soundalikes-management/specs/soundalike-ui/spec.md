## ADDED Requirements

### Requirement: Display soundalike groups with song comparison

The system SHALL provide a UI page that displays soundalike groups detected by acoustic fingerprinting with comprehensive song metadata comparison.

#### Scenario: User navigates to soundalike page
- **WHEN** user navigates to audit rule detail page for the soundalike rule (ID 9)
- **THEN** the soundalike custom page component is rendered inline
- **AND** page loads soundalike groups from API
- **AND** displays groups sorted by match score (highest first)

#### Scenario: Display song metadata for comparison
- **WHEN** soundalike group is displayed
- **THEN** each song shows: artwork preview, title, artists, album, year, genres, has lyrics indicator, file size, bitrate, duration, created date
- **AND** artwork preview shows thumbnail on hover
- **AND** songs are displayed side-by-side or in a comparable layout

#### Scenario: Show match confidence
- **WHEN** soundalike group is displayed
- **THEN** match score percentage is shown for the group
- **AND** pairwise match scores between songs are available for inspection

### Requirement: User selects primary and secondary songs

The system SHALL allow users to select which song to keep (primary) and which to delete (secondaries).

#### Scenario: Select primary song
- **WHEN** user clicks on a song in a group
- **THEN** song is marked as primary (to keep)
- **AND** primary song is visually distinguished (highlighted or bordered)
- **AND** only one song per group can be primary

#### Scenario: Select secondary songs
- **WHEN** user clicks additional songs after selecting primary
- **THEN** songs are marked as secondary (to delete)
- **AND** secondary songs are visually marked (strikethrough, faded, or checkbox)

#### Scenario: Change primary selection
- **WHEN** user selects a different song as primary
- **THEN** previous primary becomes unselected
- **AND** new song becomes primary
- **AND** previous secondary selections remain

#### Scenario: Deselect secondary song
- **WHEN** user clicks a secondary song
- **THEN** song is deselected
- **AND** returns to neutral state

### Requirement: TypeScript type safety for data column

The system SHALL use TypeScript type guards and discriminated unions to safely handle the Data column from audit non-conformities.

#### Scenario: Type guard validates soundalike group data
- **WHEN** API returns audit non-conformity data
- **THEN** type guard function validates that data has songIds array and matchScore
- **AND** TypeScript compiler enforces correct type usage

#### Scenario: Handle invalid data gracefully
- **WHEN** data fails type guard validation
- **THEN** component displays error message or skips invalid group
- **AND** does not crash the entire page

### Requirement: Remove Duplicates button processes selected groups

The system SHALL provide a Remove Duplicates button that resolves all groups where a primary song is selected.

#### Scenario: Remove Duplicates button shows count
- **WHEN** user has selected primary songs in N groups
- **THEN** button displays "Remove Duplicates (N)"
- **AND** button is disabled if no groups have primary selection

#### Scenario: Click Remove Duplicates
- **WHEN** user clicks Remove Duplicates button
- **THEN** confirmation dialog appears showing count of songs to delete
- **AND** user must confirm to proceed

#### Scenario: Confirm removal
- **WHEN** user confirms removal
- **THEN** API call is made to resolve endpoint with all selected groups
- **AND** loading state is shown during processing

#### Scenario: Removal succeeds
- **WHEN** API returns success
- **THEN** resolved groups are removed from display
- **AND** success message appears
- **AND** Remove Duplicates button updates count

#### Scenario: Removal fails
- **WHEN** API returns error
- **THEN** error message displays
- **AND** groups remain in current state
- **AND** user can retry

### Requirement: Preview metadata merge before deletion

The system SHALL show users what metadata will be merged before confirming deletion.

#### Scenario: Show merge preview
- **WHEN** user has selected primary and secondary songs in a group
- **THEN** preview panel shows which missing fields will be filled from secondaries
- **AND** highlights fields that will change on primary

#### Scenario: Confirm shows final state
- **WHEN** confirmation dialog appears
- **THEN** dialog shows merged metadata summary for primary song
- **AND** shows which songs will be deleted
