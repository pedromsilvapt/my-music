## ADDED Requirements

### Requirement: CLI Orchestrator supports SyncDirection
The CLI orchestrator SHALL check SyncDirection before executing upload and server actions phases, skipping phases that don't apply to the direction.

#### Scenario: Orchestrator skips upload for SyncDirection.Down
- **WHEN** SyncDirection is Down
- **THEN** uploadPhase is not called
- **AND** serverActionsPhase still executes (downloads only)

#### Scenario: Orchestrator skips server actions for SyncDirection.Up
- **WHEN** SyncDirection is Up
- **THEN** serverActionsPhase is not called
- **AND** uploadPhase executes normally

#### Scenario: Orchestrator executes all phases for SyncDirection.Both
- **WHEN** SyncDirection is Both
- **THEN** all phases execute in order
- **AND** behavior is identical to pre-refactor SyncService

### Requirement: CLI Orchestrator reports progress via IProgress
The CLI orchestrator SHALL report sync progress via `IProgress<SyncProgress>` interface for console output.

#### Scenario: Progress reports current phase and counts
- **WHEN** sync is running
- **THEN** IProgress.Report is called with current phase name, processed count, and total count
- **AND** CLI can display progress bar or status text
