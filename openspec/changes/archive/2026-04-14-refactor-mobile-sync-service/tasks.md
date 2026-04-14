## 1. Test Infrastructure Setup

- [x] 1.1 Install Jest, ts-jest, @types/jest in MyMusic.Mobile and configure jest.config.ts with TypeScript support
- [x] 1.2 Add `test` script to MyMusic.Mobile/package.json
- [x] 1.3 Verify Jest runs a dummy test successfully (`npm test` exits 0)

## 2. Shared Scanner Types & Utils Extraction

- [x] 2.1 Create `MyMusic.Mobile/src/services/scanner/types.ts` with shared `FileMetadata`, `ScanError`, `ScanOptions`, `ScanResult` interfaces (copied from fileScanner.ts, removing duplicates)
- [x] 2.2 Create `MyMusic.Mobile/src/services/scanner/utils.ts` with shared `fromEpochTimestamp`, `shouldExclude`, `globToRegex`, `escapeRegExp`, `yieldToUI` functions (copied from fileScanner.ts)
- [x] 2.3 Update `fileScanner.ts` to import types and utils from shared modules, remove local duplicates
- [x] 2.4 Update `mediaLibraryScanner.ts` to import types and utils from shared modules, remove local duplicates
- [x] 2.5 Update `scannerRegistry.ts` to import `ScannerFunction` type from shared `types.ts` instead of inline
- [x] 2.6 Verify TypeScript compiles and existing app behavior is unchanged

## 3. Interface Definitions

- [x] 3.1 Create `MyMusic.Mobile/src/services/sync/types.ts` with `SyncContext`, `SyncResult`, `ConflictResolution`, `RecordItem` types, and the `SyncDeps` interface
- [x] 3.2 Create `MyMusic.Mobile/src/services/sync/interfaces.ts` with `ISyncApiClient`, `ISyncConfig`, `ISyncState`, `IFileSystemScanner`, `IFileOps`, `IKeepAwake`, `IUserPrompt` interfaces
- [x] 3.3 Verify TypeScript compiles with the new interfaces (they won't be used yet, just defined)

## 4. Production Interface Implementations (Defaults)

- [x] 4.1 Create `MyMusic.Mobile/src/services/sync/defaults.ts` with production implementations that delegate to existing code: `createDefaultApiClient()`, `createDefaultConfig()`, `createDefaultState()`, `createDefaultScanner()`, `createDefaultFileOps()`, `createDefaultKeepAwake()`, `createDefaultUserPrompt()`
- [x] 4.2 Verify TypeScript compiles and each default correctly wraps the real implementation

## 5. Utility Functions Extraction

- [x] 5.1 Move `safeToIsoString` and `chunkArray` and `formatFilePath` from syncService.ts into `src/services/sync/utils.ts`
- [x] 5.2 Update syncService.ts to import from the new utils location
- [x] 5.3 Verify TypeScript compiles and app behavior is unchanged

## 6. Atomic Operations Extraction

- [x] 6.1 Create `MyMusic.Mobile/src/services/sync/atomic-operations.ts` with `uploadOneFile` function (extracted from the toCreate/toUpdate upload loops in runSync)
- [x] 6.2 Create `downloadOneFile` function (extracted from the Download action handling in runSync)
- [x] 6.3 Create `removeOneFile` function (extracted from the Remove action handling in runSync)
- [x] 6.4 Update runSync to call the extracted atomic operations instead of inline code
- [x] 6.5 Verify TypeScript compiles and app behavior is unchanged

## 7. Phase Functions Extraction

- [x] 7.1 Create `MyMusic.Mobile/src/services/sync/phases.ts` with `scanPhase` function (extracted from the scanning section of runSync)
- [x] 7.2 Create `startSessionPhase` function (extracted from the session start + scan error recording section)
- [x] 7.3 Create `resolveConflictsPhase` function (extracted from the conflict detection/resolution logic within the chunk loop)
- [x] 7.4 Create `uploadPhase` function (extracted from the chunk loop: check → resolve → upload → record)
- [x] 7.5 Create `serverActionsPhase` function (extracted from the pending actions loop: download/remove)
- [x] 7.6 Create `completePhase` function (extracted from the session completion + metadata saving section)
- [x] 7.7 Update runSync to call each extracted phase function instead of inline code
- [x] 7.8 Verify TypeScript compiles and app behavior is unchanged

## 8. Orchestrator & Context

- [x] 8.1 Create `MyMusic.Mobile/src/services/sync/context.ts` with `createSyncContext` function that initializes SyncContext from ISyncConfig and ISyncState
- [x] 8.2 Create `MyMusic.Mobile/src/services/sync/orchestrator.ts` with `orchestrateSync(deps, onProgress)` that composes all phases, handles SyncCancelledError, and ensures keepAwake.deactivate() in finally
- [x] 8.3 Update runSync to construct production SyncDeps, create SyncContext, and delegate to orchestrateSync
- [x] 8.4 Verify TypeScript compiles and app behavior is unchanged

## 9. Unit Tests — Atomic Operations

- [x] 9.1 Create test file `MyMusic.Mobile/src/services/sync/__tests__/atomic-operations.test.ts`
- [x] 9.2 Test `uploadOneFile` success case (apiClient.uploadFile resolves, returns 'Created' record)
- [x] 9.3 Test `uploadOneFile` failure case (apiClient.uploadFile rejects, returns 'Error' record)
- [x] 9.4 Test `downloadOneFile` success case (downloads, writes, acknowledges)
- [x] 9.5 Test `downloadOneFile` replaces existing file (deletes existing before write)
- [x] 9.6 Test `downloadOneFile` creates parent directory (when missing)
- [x] 9.7 Test `downloadOneFile` failure case (increments result.failed, returns 'Error' record)
- [x] 9.8 Test `removeOneFile` with user confirmation (deletes, acknowledges)
- [x] 9.9 Test `removeOneFile` with user cancellation (no delete, no acknowledge)
- [x] 9.10 Test `removeOneFile` with autoConfirm (skips prompt, deletes)
- [x] 9.11 Test `removeOneFile` with missing file (still acknowledges, no record)
- [x] 9.12 Verify all tests pass (`npm test`)

## 10. Unit Tests — Phase Functions

- [x] 10.1 Create test file `MyMusic.Mobile/src/services/sync/__tests__/phases.test.ts`
- [x] 10.2 Test `resolveConflictsPhase` auto-resolved conflicts (adds to toUpdate set)
- [x] 10.3 Test `resolveConflictsPhase` treatConflictsAsErrors (increments failed, no prompt)
- [x] 10.4 Test `resolveConflictsPhase` user prompt for 'upload' (adds to toUpdate)
- [x] 10.5 Test `resolveConflictsPhase` user prompt for 'skip' (increments failed)
- [x] 10.6 Test `resolveConflictsPhase` dry-run (counts conflicts, no API call, no prompt)
- [x] 10.7 Test `completePhase` authoritative server counts override client estimates
- [x] 10.8 Test `completePhase` saves lastSyncAt and lastScanTotal
- [x] 10.9 Verify all tests pass (`npm test`)

## 11. Unit Tests — Orchestrator

- [x] 11.1 Create test file `MyMusic.Mobile/src/services/sync/__tests__/orchestrator.test.ts`
- [x] 11.2 Test full sync executes all phases in order (mock all deps, verify call sequence)
- [x] 11.3 Test cancellation returns partial result with cancelled=true
- [x] 11.4 Test error propagation after cleanup
- [x] 11.5 Test keepAwake.deactivate always called (success, error, cancellation paths)
- [x] 11.6 Verify all tests pass (`npm test`)

## 12. Cleanup & Final Verification

- [x] 12.1 Remove any dead code from original syncService.ts (the file should now be just the backward-compatible `runSync` export + `fetchSyncHistory` + `fetchSessionDetails`)
- [x] 12.2 Verify TypeScript compiles cleanly with no errors
- [x] 12.3 Run all tests and verify they pass (`npm test`)
- [x] 12.4 Manual smoke test: run the mobile app, start a sync, verify it works end-to-end