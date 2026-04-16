## Context

CLI `SyncService.cs` is a 742-line monolith containing all sync logic: orchestration, phase coordination, file scanning, conflict resolution, upload/download operations, and progress reporting. This was the original implementation before Mobile was refactored into a modular architecture with dependency injection.

Mobile now has a clean separation:
- `types.ts` - Interfaces and type definitions
- `interfaces.ts` - Platform abstraction interfaces
- `orchestrator.ts` - High-level phase coordination (38 lines)
- `phases.ts` - Individual phase functions (482 lines)
- `atomic-operations.ts` - Low-level file operations (155 lines)
- `context.ts` - SyncContext factory (31 lines)
- `defaults.ts` - Platform-specific implementations (155 lines)
- `utils.ts` - Helper functions (34 lines)

The goal is to replicate this structure in CLI while extracting shared interfaces to `MyMusic.Common` for reuse.

## Goals / Non-Goals

**Goals:**
- Refactor CLI SyncService into modular files matching Mobile structure
- Extract shared interfaces to MyMusic.Common for reuse across CLI and Mobile
- Enable dependency injection for testability
- Support CLI-only SyncDirection feature (Both/Up/Down)
- Document CLI-Mobile differences with @TODO comments in Mobile

**Non-Goals:**
- No behavioral changes to sync logic
- No API changes
- No Mobile refactoring (already done)
- No shared implementation code between CLI and Mobile (only interfaces)

## Decisions

### Decision 1: File Structure

**Choice:** Create `MyMusic.CLI/Services/Sync/` folder with 7 files matching Mobile.

**Rationale:** One-to-one mapping makes it easy for developers to navigate both codebases. File purposes are identical across platforms.

**Alternatives considered:**
- Fewer files (combine some) → Would lose the clear separation that makes Mobile testable
- Different naming → Would create confusion when comparing platforms

### Decision 2: Shared Interfaces Location

**Choice:** Move interfaces to `MyMusic.Common/Services/Sync/` namespace.

**Rationale:** MyMusic.Common is already the shared library for entities, services, and EF Core. It's the natural home for cross-platform interfaces.

**Alternatives considered:**
- Keep in CLI only → Mobile would need duplicate definitions
- Create new MyMusic.Sync project → Overkill for just interfaces

### Decision 3: SyncDirection Implementation

**Choice:** Add `SyncDirection` enum to `SyncOptions` with CLI-only comments. Orchestrator skips phases based on direction.

**Rationale:** CLI users want fine-grained control (upload-only, download-only, or both). Mobile doesn't need this (always sync both ways).

**Alternatives considered:**
- Add to Mobile too → Unnecessary feature for mobile
- Runtime configuration → CLI already has command-line parsing

### Decision 4: IFileSystemScanner CLI-Specific

**Choice:** Keep `IFileSystemScanner` as CLI-specific interface (not shared).

**Rationale:** Mobile uses `expo-media-library` or `expo-file-system` scanners with different signatures. CLI uses `IFileScanner` from TagLib.

**Alternatives considered:**
- Generic scanner interface → Would require adapter pattern, adds complexity

### Decision 5: IKeepAwake CLI Implementation

**Choice:** CLI implements `IKeepAwake` as no-op (empty methods).

**Rationale:** CLI doesn't need screen wake lock. Implementing as no-op allows sharing the orchestrator code without special cases.

**Alternatives considered:**
- Remove from CLI interface → Would require separate orchestrator code paths

## Risks / Trade-offs

**Risk:** Behavior divergence between CLI and Mobile
→ Mitigation: Both platforms implement the same interfaces with the same spec requirements

**Risk:** Shared interfaces become lowest-common-denominator
→ Mitigation: Interfaces are designed for full sync feature set; CLI-only features documented with @TODO

**Risk:** Large refactoring could introduce bugs
→ Mitigation: Phased implementation with tests at each stage; existing sync behavior preserved

**Risk:** SyncDirection logic adds complexity to phases
→ Mitigation: Early return patterns in phase functions; orchestrator handles direction filtering

## Migration Plan

1. **Phase 1:** Create shared interfaces in MyMusic.Common (no behavior change)
2. **Phase 2:** Create CLI Sync/ folder structure with Types.cs
3. **Phase 3:** Extract Context.cs and Defaults.cs (DI setup)
4. **Phase 4:** Extract AtomicOperations.cs (low-risk, pure functions)
5. **Phase 5:** Extract Phases.cs (moderate risk, test each phase)
6. **Phase 6:** Create Orchestrator.cs (high risk, integration)
7. **Phase 7:** Add SyncDirection feature
8. **Phase 8:** Add @TODO comments to Mobile

**Rollback:** Each phase is a single commit. Revert commits to restore previous state.

## Open Questions

None - the refactoring plan is fully specified from the approved review thread.
