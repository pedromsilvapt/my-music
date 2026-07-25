# Refactor: Sync & Devices Controllers

**Goal:** Thin controllers (input/output/validation/DTO mapping only); business logic in per-operation services under `MyMusic.Common/Services/Devices/` and `MyMusic.Common/Services/Sync/`. Routes must remain unchanged. Max ~500 lines/file. One operation per service.

## Source

- `MyMusic.Server/Controllers/DevicesController.cs` — 1627 lines mixing device CRUD, sync session lifecycle, and sync workflow operations with heavy business logic.

## Target controller layout (3 controllers, same `[Route("devices")]`)

- **DevicesController** — device CRUD + device filter metadata/values
- **SyncSessionsController** — session lifecycle, records, session filter metadata/values
- **SyncController** — sync workflow (start/complete/cancel/commit/check/resolve/upload/error/acknowledge/pending-actions/device-songs)

## Target service layout

```
MyMusic.Common/Services/Devices/
  IDeviceListService.cs, DeviceListService.cs
  IDeviceGetService.cs,  DeviceGetService.cs
  IDeviceCreateService.cs, DeviceCreateService.cs
  IDeviceUpdateService.cs, DeviceUpdateService.cs
  IDeviceDeleteService.cs, DeviceDeleteService.cs
  IDeviceFilterValuesService.cs, DeviceFilterValuesService.cs
  IDeviceLookupService.cs, DeviceLookupService.cs   (FindDeviceAsync shared)

MyMusic.Common/Services/Sync/
  (existing) ISyncCommitService, ISyncUploadService, ISyncActionsServerFactory, StagingDirectoryCleanupService
  ISyncSessionLookupService.cs, SyncSessionLookupService.cs  (FindSession/GetActiveSession shared)
  ISyncPathResolver.cs, SyncPathResolver.cs  (ComputePendingActionPath, GetUniquePath)
  ISyncComparisonHelper.cs, SyncComparisonHelper.cs  (IsNewerThan)
  ISyncSessionListService.cs, SyncSessionListService.cs
  ISyncSessionRecordsQueryService.cs, SyncSessionRecordsQueryService.cs
  ISyncSessionFilterValuesService.cs, SyncSessionFilterValuesService.cs
  ISyncSessionDeleteService.cs, SyncSessionDeleteService.cs
  ISyncSessionPruneService.cs, SyncSessionPruneService.cs
  ISyncStartService.cs, SyncStartService.cs
  ISyncCompleteService.cs, SyncCompleteService.cs
  ISyncCancelService.cs, SyncCancelService.cs
  ISyncPendingActionsService.cs, SyncPendingActionsService.cs
  ISyncDeviceSongsService.cs, SyncDeviceSongsService.cs
  ISyncCheckService.cs, SyncCheckService.cs
  ISyncResolveConflictsService.cs, SyncResolveConflictsService.cs
  ISyncReportErrorService.cs, SyncReportErrorService.cs
  ISyncAcknowledgeService.cs, SyncAcknowledgeService.cs
```

Filter-metadata endpoints (pure static output) stay in their controllers.

## Phased implementation

Each phase: write/update tests first, implement, run tests, update status flag.

- [x] **Phase 0 — Shared helpers.** Extract `IDeviceLookupService` (FindDeviceAsync), `ISyncSessionLookupService` (FindSessionAsync, GetActiveSessionAsync), `ISyncPathResolver` (ComputePendingActionPath, GetUniquePath), `ISyncComparisonHelper` (IsNewerThan). Register in DI. DevicesController keeps using them via injection until its own phases run.
- [x] **Phase 1 — Device List & Get.** `DeviceListService`, `DeviceGetService`. DevicesController.List/Get become thin.
- [x] **Phase 2 — Device Create & Update.** `DeviceCreateService`, `DeviceUpdateService`.
- [x] **Phase 3 — Device Delete.** `DeviceDeleteService` (device + sessions + SongDevices + session records + staging cleanup).
- [x] **Phase 4 — Device filter values.** `DeviceFilterValuesService` (filter metadata stays in controller).
- [x] **Phase 5 — SyncSessionsController split + ListSessions.** Create `SyncSessionsController`; extract `SyncSessionListService`. Move `ListSessions` endpoint.
- [x] **Phase 6 — Session records query & filter values.** `SyncSessionRecordsQueryService`, `SyncSessionFilterValuesService`. Move records/filter-metadata/filter-values endpoints.
- [x] **Phase 7 — Session delete & prune.** `SyncSessionDeleteService`, `SyncSessionPruneService`.
- [x] **Phase 8 — SyncController split + Start & Complete.** Create `SyncController`; `SyncStartService`, `SyncCompleteService`. Move start/complete endpoints.
- [x] **Phase 9 — Cancel & Commit.** `SyncCancelService`; thin `CommitSync` delegating to existing `ISyncCommitService` + `ISyncSessionLookupService` + staging cleanup (MapCommitResponse helpers move to a `SyncCommitResponseMapper` static or DTO).
- [x] **Phase 10 — Pending actions & device songs.** `SyncPendingActionsService` (absorbs `CreatePendingActionsForDevice` + naming helpers via `ISyncPathResolver`), `SyncDeviceSongsService`.
- [x] **Phase 11 — Sync Check.** `SyncCheckService` (the ~280-line method; uses `ISyncPathResolver`, `ISyncComparisonHelper`, `ISyncActionsServerFactory`).
- [x] **Phase 12 — Resolve Conflicts.** `SyncResolveConflictsService` (the ~190-line method).
- [x] **Phase 13 — Report error & Acknowledge.** `SyncReportErrorService`, `SyncAcknowledgeService` (acknowledge wraps existing `ISyncCommitService.AcknowledgeRecordsAsync`).
- [ ] **Phase 14 — Final cleanup.** Remove leftover private helpers from controllers, verify all routes unchanged, run full unit test suite + targeted integration tests, confirm no file >500 lines.

## Verification per phase

1. Update/keep existing `DevicesControllerSpecs.*` tests green (adjust constructor args as services are introduced).
2. Add service-level specs for each new service (`MyMusic.Common.Tests/Services/Devices/`, `.../Sync/`).
3. `dotnet build` + `dotnet test MyMusic.Common.Tests` after each phase.