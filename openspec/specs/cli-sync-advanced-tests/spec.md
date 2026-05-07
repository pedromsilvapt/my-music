## ADDED Requirements

### Requirement: Sync Direction Flags Test
The integration test suite SHALL verify that the `--direction` CLI flag controls sync direction.

#### Scenario: Direction up should upload without downloading
- **WHEN** a local song exists and a server song is assigned to the device
- **AND** the CLI sync command is run with `--direction up`
- **THEN** the local song SHALL be uploaded to the server
- **AND** the server song SHALL NOT be downloaded to the device

#### Scenario: Direction down should download without uploading
- **WHEN** a local song exists and a server song is assigned to the device
- **AND** the CLI sync command is run with `--direction down`
- **THEN** the server song SHALL be downloaded to the device
- **AND** the local song SHALL NOT be uploaded to the server

### Requirement: Dry-Run Mode Test
The integration test suite SHALL verify that `--dry-run` reports changes without executing them.

#### Scenario: Dry-run reports creation without uploading
- **WHEN** a local song exists and the CLI sync command is run with `--dry-run`
- **THEN** the result SHALL report `Created: 1`
- **AND** the song SHALL NOT appear on the server
- **AND** a subsequent real sync SHALL upload the song successfully

#### Scenario: Dry-run reports download without downloading
- **WHEN** a server song is assigned to the device and the CLI sync command is run with `--dry-run`
- **THEN** the result SHALL report `Downloaded: 1`
- **AND** the file SHALL NOT appear locally
- **AND** a subsequent real sync SHALL download the file successfully

### Requirement: Force Mode Test
The integration test suite SHALL verify that `--force` re-uploads unchanged files.

#### Scenario: Force re-uploads unchanged files
- **WHEN** a local song is synced normally (idempotent on second sync)
- **AND** the CLI sync command is run with `--force`
- **THEN** the song SHALL be re-uploaded (`Updated >= 1`)

### Requirement: Server-Initiated Remove Test
The integration test suite SHALL verify that server-initiated removals propagate to the device.

#### Scenario: Server marks device for removal
- **WHEN** a song is synced to the device
- **AND** the device is marked for removal via `ManageSongDevicesFlow`
- **AND** the CLI sync command is run again
- **THEN** the local file SHALL be deleted
- **AND** the device association SHALL be removed

#### Scenario: Server song deleted
- **WHEN** a song is synced to the device
- **AND** the song is deleted from the server via API
- **AND** the CLI sync command is run again
- **THEN** the local file SHALL be removed

### Requirement: Conflict Resolution Test
The integration test suite SHALL verify conflict detection and auto-resolution.

#### Scenario: Auto-resolve when content identical
- **WHEN** a synced song is modified locally with the same change on the server
- **AND** the CLI sync command is run
- **THEN** the conflict SHALL be auto-resolved
- **AND** no data loss SHALL occur

#### Scenario: Report conflict when content differs
- **WHEN** a synced song has a different local title than the server title
- **AND** the CLI sync command is run
- **THEN** the result SHALL report `Conflicts >= 1`
- **AND** neither side SHALL be overwritten

### Requirement: Multi-Song Combination Test
The integration test suite SHALL verify sync behavior with multiple simultaneous operations.

#### Scenario: Multiple uploads in one sync
- **WHEN** 3 local songs exist and a single sync is run
- **THEN** all 3 songs SHALL appear on the server with correct metadata

#### Scenario: Mixed operations in one sync
- **WHEN** one local song is new, one is locally modified, one has server changes, and one is locally deleted
- **AND** a single sync is run
- **THEN** all 4 operations SHALL succeed with correct outcomes for each

#### Scenario: Multiple server downloads in one sync
- **WHEN** 3 songs are seeded on the server all assigned to the device
- **AND** a single sync is run
- **THEN** all 3 files SHALL be downloaded with correct metadata

#### Scenario: Multiple server removes in one sync
- **WHEN** 3 songs are synced to the device and then all marked for removal on the server
- **AND** a single sync is run
- **THEN** all 3 local files SHALL be deleted

### Requirement: Metadata Edge Case Test
The integration test suite SHALL verify file renames when album or artist changes on the server.

#### Scenario: File renamed when album changes
- **WHEN** a synced song's album is changed on the server
- **AND** the CLI sync command is run
- **THEN** the local file SHALL be moved to a new album directory

#### Scenario: File renamed when artist changes
- **WHEN** a synced song's artist is changed on the server
- **AND** the CLI sync command is run
- **THEN** the local file SHALL be renamed with the new artist

### Requirement: Sequential Sync Test
The integration test suite SHALL verify state correctness across consecutive syncs.

#### Scenario: Successive title changes across syncs
- **WHEN** a song title is changed to "A" on the server and synced
- **AND** the title is then changed to "B" on the server and synced again
- **THEN** the final local state SHALL reflect title "B"

#### Scenario: Upload then update then download across syncs
- **WHEN** a local song is created and synced (upload)
- **AND** the song is modified locally and synced again (update)
- **AND** the song is modified on the server and synced again (download)
- **THEN** the final state SHALL reflect the server-side change