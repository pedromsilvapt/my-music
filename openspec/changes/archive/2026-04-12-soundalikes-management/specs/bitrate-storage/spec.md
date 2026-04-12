## ADDED Requirements

### Requirement: Song entity stores bitrate

The system SHALL store bitrate information in the Song entity for each imported song.

#### Scenario: Song has bitrate field
- **WHEN** Song entity is created or updated
- **THEN** bitrate is stored as nullable integer property
- **AND** database schema includes Bitrate column in Songs table

#### Scenario: Existing songs have null bitrate
- **WHEN** songs exist before bitrate feature is added
- **THEN** their bitrate field is null
- **AND** songs continue to function normally
- **AND** display shows "Unknown" or empty for bitrate

### Requirement: Import extracts bitrate from audio files

The system SHALL extract bitrate from audio files during the music import process.

#### Scenario: Extract bitrate from MP3 file
- **WHEN** music service imports an MP3 file
- **THEN** TagLib extracts AudioBitrate from file properties
- **AND** bitrate is stored in Song entity
- **AND** bitrate is measured in kilobits per second (kbps)

#### Scenario: Extract bitrate from FLAC file
- **WHEN** music service imports a FLAC file
- **THEN** TagLib extracts AudioBitrate from file properties
- **AND** bitrate is stored in Song entity

#### Scenario: Bitrate extraction fails gracefully
- **WHEN** TagLib cannot determine bitrate for a file
- **THEN** bitrate is stored as null
- **AND** warning is logged
- **AND** import continues without failure

#### Scenario: Re-import updates bitrate
- **WHEN** existing song is re-imported or metadata is refreshed
- **THEN** bitrate is updated if previously null or changed
- **AND** other song metadata is preserved

### Requirement: Database migration adds bitrate column

The system SHALL provide a migration that adds the bitrate column to the Songs table.

#### Scenario: Migration is reversible
- **WHEN** migration is applied
- **THEN** Bitrate column is added as nullable integer
- **WHEN** migration is rolled back
- **THEN** Bitrate column is removed
- **AND** no data loss occurs for other columns

#### Scenario: Migration does not require data backfill
- **WHEN** migration is applied to existing database
- **THEN** existing songs retain null bitrate
- **AND** no mandatory backfill script is required
- **AND** users can optionally re-import to populate bitrate
