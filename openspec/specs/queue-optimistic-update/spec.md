## ADDED Requirements

### Requirement: Play Next moves existing songs in queue
When songs are added to the queue with "Play Next" and one or more of those songs already exist in the queue, the frontend SHALL move those songs to the position after the currently playing song rather than duplicating them.

#### Scenario: Play Next with songs already in queue
- **WHEN** a user selects songs D and E that are already in queue [A, B, C, D, E] and clicks "Play Next" while B is currently playing
- **THEN** the queue immediately displays [A, B, D, E, C] with D and E moved after B, not duplicated

#### Scenario: Play Next with mix of new and existing songs
- **WHEN** a user selects songs [D, F] where D is in queue but F is not, and clicks "Play Next" while B is currently playing
- **THEN** the queue immediately displays with D moved and F added, both positioned after the current song

### Requirement: Play Last moves existing songs in queue
When songs are added to the queue with "Play Last" and one or more of those songs already exist in the queue, the frontend SHALL move those songs to the end of the queue rather than duplicating them.

#### Scenario: Play Last with songs already in queue
- **WHEN** a user selects songs D and E that are already in queue [A, B, C, D, E] and clicks "Play Last" while B is currently playing
- **THEN** the queue immediately displays [A, B, C, D, E] with D and E moved to the end, not duplicated

#### Scenario: Play Last with mix of new and existing songs
- **WHEN** a user selects songs [D, F] where D is in queue but F is not, and clicks "Play Last"
- **THEN** the queue immediately displays with D moved and F added, both positioned at the end of the queue

### Requirement: Optimistic update matches backend behavior
The optimistic update performed by `playNext()` and `playLast()` SHALL produce the same queue state as the backend `AddToQueue` endpoint for both existing songs (moved) and new songs (added).

#### Scenario: Optimistic update consistency
- **WHEN** the frontend performs an optimistic update for Play Next or Play Last
- **THEN** the resulting queue state in the React Query cache SHALL be identical to the state returned by the backend after the mutation completes
