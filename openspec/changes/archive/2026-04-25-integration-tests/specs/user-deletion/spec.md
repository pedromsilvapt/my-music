## ADDED Requirements

### Requirement: User deletion by ID

The system SHALL allow deletion of a user by their numeric ID via the `DELETE /users/{id}` endpoint.

#### Scenario: Delete existing user
- **WHEN** client sends `DELETE /users/{id}` with valid user ID
- **THEN** system returns 200 OK with deleted user info
- **AND** user is removed from database
- **AND** all owned entities are cascade deleted
- **AND** user's music repository directory is deleted

#### Scenario: Delete non-existent user
- **WHEN** client sends `DELETE /users/{id}` with non-existent user ID
- **THEN** system returns 404 Not Found

### Requirement: Cascade deletion of owned entities

The system SHALL cascade delete all entities owned by the deleted user.

#### Scenario: Owned entities are deleted
- **WHEN** user is deleted
- **THEN** all Songs, Albums, Artists, Genres, Playlists, Devices, WishlistItems, PlayHistory, SongAcousticFingerprints, AuditNonConformities, ExcludedDuplicatePairs, and PurchasedSongs owned by user are deleted

### Requirement: Music repository file cleanup

The system SHALL delete the user's music repository directory when a user is deleted.

#### Scenario: User music directory is deleted
- **WHEN** user is deleted
- **THEN** directory at `{MusicRepositoryPath}/{username}/` is recursively deleted
- **AND** operation succeeds even if directory does not exist

### Requirement: Current queue reference cleared

The system SHALL clear the user's `CurrentQueueId` before deletion to handle the circular reference.

#### Scenario: CurrentQueue reference is cleared
- **WHEN** user with CurrentQueueId set is deleted
- **THEN** CurrentQueueId is set to null before user deletion
- **AND** the referenced queue playlist is deleted via cascade
