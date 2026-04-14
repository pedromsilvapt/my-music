## Context

`MyMusic.Mobile/src/services/syncService.ts` contains a 719-line `runSync` function that orchestrates the entire bidirectional music sync pipeline. It directly imports:

- **React Native `Alert`** — for conflict resolution and deletion confirmation prompts
- **`expo-file-system` `File`** — for local file existence checks, reads, writes, deletes
- **`expo-keep-awake`** — for preventing screen sleep during sync
- **8 API functions** from `../api/sync` — `startSync`, `checkSync`, `uploadFile`, `recordChunk`, `completeSync`, `getPendingActions`, `acknowledgeAction`, `resolveConflicts`, `downloadSong`
- **`useSyncStore`** (Zustand) — for reading cancellation state and sync options
- **`configService`** — 7 separate getter functions for device ID, repo path, extensions, etc.

No unit tests exist. The function mixes orchestration logic with I/O, UI, and state management, making it impossible to test any path without a running device and server.

The scanner implementations (`fileScanner.ts`, `mediaLibraryScanner.ts`) already share a structural interface through `scannerRegistry.ts` but duplicate `FileMetadata`, `ScanError`, `ScanOptions`, `ScanResult` types and helper functions (`shouldExclude`, `globToRegex`, `escapeRegExp`, `fromEpochTimestamp`, `yieldToUI`).

## Goals / Non-Goals

**Goals:**
- Make all sync logic unit-testable by extracting interfaces for every external dependency
- Decompose `runSync` into small, named functions with single responsibilities
- Create atomic operations (upload-one, download-one, remove-one) as reusable building blocks
- Ensure every refactoring step compiles and runs independently — no big-bang rewrite
- Preserve 100% of existing observable behavior

**Non-Goals:**
- Changing any sync behavior, API contracts, or DTOs
- Adding new sync features or phases
- Refactoring the backend (server-side sync logic)
- Refactoring the scanner implementations themselves (only extracting shared types)
- Refactoring the `syncStore` or `configService` implementations (only abstracting behind interfaces)
- Setting up a full integration test harness (only unit tests for sync logic)

## Decisions

### 1. Interface extraction pattern — interfaces as function signatures, not class-heavy DI

**Decision**: Define each interface as a TypeScript interface with method signatures. The existing functions become the default implementations. No DI container — the orchestrator accepts dependencies as a plain object parameter.

**Rationale**: React Native/Expo projects typically avoid heavy DI frameworks. A simple dependencies object is idiomatic, easy to mock, and doesn't require framework changes. The existing `scannerRegistry` pattern already follows this approach.

**Alternative considered**: Class-based services with constructor injection. Rejected because it would require rewriting all API functions and store accessors as class methods — a much larger change with no behavioral benefit.

### 2. File organization — `src/services/sync/` module directory

**Decision**: Create a `src/services/sync/` directory containing:
- `types.ts` — shared types (SyncContext, SyncResult, conflict types, record types)
- `interfaces.ts` — all dependency interfaces (ISyncApiClient, ISyncState, ISyncConfig, IFileSystemScanner, IFileOps, IKeepAwake, IUserPrompt)
- `context.ts` — SyncContext construction from config + session state
- `atomic-operations.ts` — individual file operations (uploadOneFile, downloadOneFile, removeOneFile)
- `phases.ts` — phase-level functions (scanPhase, uploadPhase, resolveConflicts, serverActionsPhase, completePhase)
- `orchestrator.ts` — the top-level `runSync` composition using injected deps
- `defaults.ts` — production dependency bindings (wire up real implementations)

**Rationale**: Co-locating related code by feature (sync) is cleaner than scattering across the existing flat `services/` directory. It makes the dependency graph explicit and each file small enough to reason about (<150 lines each).

**Alternative considered**: Keeping everything in `syncService.ts` but with extracted functions. Rejected because the file would still be 500+ lines and the interfaces would pollute the export surface.

### 3. SyncContext as explicit state carrier

**Decision**: Create a mutable `SyncContext` object that holds:
- `deviceId`, `repositoryPath`, `decodedRepoPath`, `sessionId`
- `options` (force, dryRun, autoConfirm, treatConflictsAsErrors)
- `result` (the accumulating `SyncResult`)
- `uploadedPaths` (Set of paths processed during upload)
- `pendingDownloadPaths` (Set of paths pending download)

Phase functions receive `ctx: SyncContext` plus their specific inputs, and return void or their specific outputs. They mutate `ctx.result` for counters.

**Rationale**: Eliminates the deeply-nested closures over mutable locals. Makes data flow visible. Each phase function's contract is explicit about what it reads and writes.

**Alternative considered**: Immutable context with return-value updates (functional style). Rejected because it would require deep merging of the result accumulator on every call, adding complexity without testability benefit.

### 4. Incremental migration strategy — Strangler Fig pattern

**Decision**: Refactor incrementally:
1. First, create interfaces and the new directory structure — no behavior change
2. Extract one phase at a time (scan, then upload-chunk, then resolve, then server-actions, then complete)
3. After each extraction, update `runSync` to delegate to the new function
4. Move `runSync` to the orchestrator only after all phases are extracted
5. Each step: compile, run existing tests (manual for mobile), proceed

**Rationale**: The user explicitly asked for incremental, one-step-at-a-time refactoring. The strangler fig approach ensures the system works after every step. If any step introduces a regression, it's isolated to that step.

**Alternative considered**: Big-bang rewrite of `syncService.ts`. Rejected — too risky, hard to review, impossible to bisect regressions.

### 5. Shared scanner types — extract to `src/services/scanner/types.ts`

**Decision**: Move the duplicated `FileMetadata`, `ScanError`, `ScanOptions`, `ScanResult` types and shared helpers (`shouldExclude`, `globToRegex`, `escapeRegExp`, `fromEpochTimestamp`, `yieldToUI`) to a shared `src/services/scanner/types.ts` and `src/services/scanner/utils.ts`. Both scanners import from there.

**Rationale**: The two scanners duplicate ~40 lines of identical types and ~50 lines of identical helpers. Extracting them eliminates a maintenance hazard and makes the `IFileSystemScanner` interface definition cleaner.

### 6. User prompt abstraction — `IUserPrompt` interface

**Decision**: Create `IUserPrompt` with two methods: `promptConflictResolution(path)` → `ConflictResolution` and `confirmDeletion(path)` → `boolean`. The production implementation wraps React Native `Alert.alert`. Tests provide synchronous stubs.

**Rationale**: The `Alert` calls are the hardest part to test and the most UI-coupled. Abstracting them makes the conflict resolution flow fully testable.

## Risks / Trade-offs

- **[Risk] Mutable SyncContext allows unintended cross-phase coupling** → Mitigation: Document which fields each phase reads/writes. Consider adding TypeScript branded types or comments to clarify ownership.

- **[Risk] Interface extraction may not cover edge cases in expo-file-system behavior** → Mitigation: The `IFileOps` interface closely mirrors actual File API usage. Production wrapper delegates directly. Edge cases (e.g., `parentDirectory.create()` on SAF URIs) should be captured in the interface.

- **[Risk] No existing tests mean refactor could silently break behavior** → Mitigation: Write characterization tests (tests that capture current behavior) before refactoring each phase. Since there's no test infrastructure, we set up Jest/Vitest first. Manual testing on device for each step adds safety.

- **[Trade-off] More files means more navigation** → The directory structure makes dependency flow explicit at the cost of more files. This is an acceptable trade-off for a 719-line monolith.

- **[Trade-off] SyncContext mutation over pure functions** → State mutation is simpler for this imperative pipeline. Pure functions would require more boilerplate for what is fundamentally a sequential, side-effect-heavy process.