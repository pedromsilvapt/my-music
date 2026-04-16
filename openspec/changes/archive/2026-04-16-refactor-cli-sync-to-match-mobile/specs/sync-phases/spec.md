## ADDED Requirements

### Requirement: Upload phase respects SyncDirection
The upload phase SHALL only execute when SyncDirection is Both or Up. When SyncDirection is Down, the phase returns immediately without processing.

#### Scenario: Upload phase skipped for Down direction
- **WHEN** SyncDirection is Down
- **THEN** uploadPhase returns without processing files
- **AND** no chunks are uploaded

### Requirement: Server actions phase respects SyncDirection
The server actions phase SHALL only process downloads when SyncDirection is Both or Down. When SyncDirection is Up, the phase returns immediately.

#### Scenario: Server actions skipped for Up direction
- **WHEN** SyncDirection is Up
- **THEN** serverActionsPhase returns without processing pending actions
- **AND** no downloads or removals occur

### Requirement: CLI uses checksum-based conflict resolution
The CLI phases SHALL use checksum-based conflict resolution (same as pre-refactor behavior), not the treatConflictsAsErrors flag used by Mobile.

#### Scenario: CLI conflict resolution via checksum
- **WHEN** a conflict is detected during upload
- **THEN** the server compares checksums to determine resolution
- **AND** matching checksums auto-resolve to 'update'
- **AND** mismatched checksums require user prompt (unless autoConfirm)
