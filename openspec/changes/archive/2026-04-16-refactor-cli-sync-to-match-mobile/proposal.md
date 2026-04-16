## Why

CLI `SyncService.cs` is a 742-line monolith that mixes orchestration, phase logic, atomic operations, and CLI-specific concerns in one file. This makes testing difficult and creates inconsistency with the newly refactored Mobile sync architecture. Refactoring to match Mobile's modular structure enables code reuse through shared interfaces, improves testability through dependency injection, and ensures both platforms evolve together.

## What Changes

- **Refactor CLI SyncService into modular structure** matching Mobile's `services/sync/` organization:
  - `Types.cs` - All interfaces and type definitions
  - `Orchestrator.cs` - High-level coordination (~38 lines)
  - `Phases.cs` - Individual phase functions (~482 lines)
  - `AtomicOperations.cs` - Low-level file operations (~155 lines)
  - `Context.cs` - Factory for SyncContext (~31 lines)
  - `Defaults.cs` - CLI-specific implementations (~155 lines)
  - `Utils.cs` - Helper functions (~34 lines)

- **Keep all interfaces in CLI project** under `MyMusic.CLI/Services/Sync/`:
  - Interfaces remain local to CLI to avoid premature abstraction
  - Mobile can later extract shared interfaces if needed
  - CLI-specific interfaces include `ISyncApiClient`, `ISyncConfig`, `ISyncState`, `IFileOps`, `IUserPrompt`, and `Types`

- **Add CLI-only SyncDirection feature** with three modes: Both (default), Up, Down

- **Add @TODO comments in Mobile** documenting CLI-specific features for future alignment:
  - `phases.ts`: CLI has SyncDirection for controlling upload/download phases
  - `syncService.ts`: CLI has verbose option
  - `types.ts`: Mobile has treatConflictsAsErrors but CLI uses checksum-based conflict resolution

## Capabilities

### New Capabilities
- `cli-sync-modular`: CLI sync refactored into modular architecture matching Mobile structure, with interfaces remaining in CLI project

### Modified Capabilities
- `sync-orchestrator`: Extend to support CLI SyncDirection orchestration (skip phases based on direction)
- `sync-phases`: Extend to support CLI SyncDirection (conditionally skip upload/download phases)
- `sync-tests`: Extend to include CLI sync unit tests

## Impact

**Affected Code:**
- `MyMusic.CLI/Services/SyncService.cs` → Refactored into `MyMusic.CLI/Services/Sync/` folder structure
- `MyMusic.CLI/Services/Sync/` - New modular files created (interfaces remain in CLI)
- `MyMusic.Mobile/src/services/sync/` - @TODO comments added

**APIs:** No API changes - internal refactoring only

**Dependencies:** No new dependencies

**Systems:** CLI and Mobile sync systems aligned for easier maintenance
