## Why

The `syncService.ts` is a 719-line monolithic function (`runSync`) that handles scanning, conflict resolution, file upload/download, UI prompts, cancellation, and session management in a single deeply-nested body. It directly imports and calls React Native's `Alert`, `expo-file-system`'s `File`, `expo-keep-awake`, Zustand store, and eight API functions — making it untestable, hard to reason about, and resistant to incremental change. No unit tests exist for any sync logic.

## What Changes

- Extract interfaces for all hard dependencies: `IFileSystemScanner` (file scanning), `ISyncApiClient` (all server HTTP calls), `ISyncState` (Zustand store reads/writes), `IKeepAwake` (screen wake lock), `IFileOps` (local file read/write/delete), and `IUserPrompt` (conflict/deletion confirmation dialogs)
- Break `runSync` into small, single-responsibility functions: one per sync phase (scan, upload, resolve, server actions, complete), plus atomic operations (uploadOneFile, downloadOneFile, removeOneFile, resolveConflict, handleConflictResolution)
- Introduce a `SyncContext` data class that threads config, session state, and accumulators through the pipeline instead of closures over mutable locals
- Create a pure orchestration layer that composes the phase functions, with all side effects behind interfaces that can be mocked in tests
- Write unit tests for each extracted function using mocked interfaces
- Ensure each refactoring step is independently compilable and runnable — no big-bang rewrite

## Capabilities

### New Capabilities
- `sync-interfaces`: Interface abstractions for sync dependencies (scanner, API client, state, file ops, keep-awake, user prompts)
- `sync-phases`: Decomposed sync phase functions (scan, upload-chunk, resolve-conflicts, server-actions, complete) and atomic operations (upload-one, download-one, remove-one)
- `sync-orchestrator`: Top-level orchestrator that composes phase functions using injected dependencies, replacing the monolithic `runSync`
- `sync-tests`: Unit tests for all decomposed sync functions using mocked interfaces

### Modified Capabilities
<!-- No existing spec-level behavior changes — this is a pure refactor that preserves all observable behavior -->

## Impact

- **`MyMusic.Mobile/src/services/syncService.ts`**: Primary file being decomposed. The `runSync` export will continue to work but will delegate to the new orchestrator
- **`MyMusic.Mobile/src/services/fileScanner.ts`** and **`mediaLibraryScanner.ts`**: Will conform to the `IFileSystemScanner` interface (already structurally compatible)
- **`MyMusic.Mobile/src/api/sync.ts`**: API calls will be accessed through `ISyncApiClient` interface
- **`MyMusic.Mobile/src/stores/syncStore.ts`**: Store access will go through `ISyncState` interface
- **`MyMusic.Mobile/src/services/configService.ts`**: Config access will go through `ISyncConfig` interface
- **New files**: `src/services/sync/` directory with interfaces, phase functions, atomic operations, orchestrator, and context types
- **No API changes**: No backend modifications, no DTO changes, no breaking changes to external contracts