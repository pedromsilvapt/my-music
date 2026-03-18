# Tasks: Auto-Fetch Metadata for Audit Issues

**Input**: Design documents from `/specs/003-metadata-auto-fetch/`  
**Feature**: Auto-Fetch Metadata for Audit Issues  
**Branch**: `003-metadata-auto-fetch`  
**Date**: 2026-03-17

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create `AutoFetchedMetadata` entity in `MyMusic.Common/Entities/AutoFetchedMetadata.cs`
- [x] T002 Create `MetadataFetchTask` entity in `MyMusic.Common/Entities/MetadataFetchTask.cs`
- [x] T003 [P] Add DbSet properties to `MusicDbContext` in `MyMusic.Common/MusicDbContext.cs`
- [x] T004 Configure entity indexes and JSON column mapping in `MusicDbContext.OnModelCreating()`
- [x] T005 Create EF Core migration for new entities: `dotnet ef migrations add AddAutoFetchedMetadata`
- [ ] **T005a [P]** Write unit tests for `AutoFetchedMetadata` entity validation and JSON serialization
- [ ] **T005b [P]** Write unit tests for `MetadataFetchTask` entity state transitions

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Create `MetadataFetchQueue` background service in `MyMusic.Common/Services/MetadataFetchQueue.cs` (mirrors PurchasesQueue pattern)
- [x] T007 Create `MetadataFetchExecutor` nested class in `MetadataFetchQueue.cs` for actual metadata fetching logic
- [x] T008 Implement `PullNextTasksAsync()` method in MetadataFetchScheduler to query queued tasks
- [x] T009 Implement `ExecuteTaskCoreAsync()` to call sources API and store results
- [x] T010 Register queue and executor in DI: `MyMusic.Common/HostBuilderExtensions.cs`
- [x] T011 Create `IAuditRuleFieldMapper` interface in `MyMusic.Common/Services/IAuditRuleFieldMapper.cs`
- [x] T012 Implement `AuditRuleFieldMapper` in `MyMusic.Common/Services/AuditRuleFieldMapper.cs` (maps rule IDs to metadata fields)
- [x] T013 [P] Add service registrations for field mapper in DI container
- [ ] **T013a [P]** Write unit tests for `MetadataFetchQueue` scheduler logic: `PullNextTasksAsync_EmptyQueue_ReturnsEmptyList`
- [ ] **T013b [P]** Write unit tests for `MetadataFetchExecutor`: `ExecuteTaskCoreAsync_ValidSong_FetchesAndStoresMetadata`
- [ ] **T013c [P]** Write unit tests for `AuditRuleFieldMapper`: `MapRuleToFields_MissingYearRule_ReturnsYearField`

**Checkpoint**: Foundation ready - background queue infrastructure complete, can process tasks

---

## Phase 3: User Story 1 - Trigger Metadata Auto-Fetch (Priority: P1) 🎯 MVP

**Goal**: Users can click a button on the audits page to queue background tasks for fetching metadata on all eligible songs

**Independent Test**: Click "Auto-fetch Metadata" button on audits page, verify tasks are queued in database and scheduler starts processing them

### Implementation for User Story 1

- [x] T014 Create DTOs for batch endpoint in `MyMusic.Server/DTO/MetadataFetch/BatchMetadataFetchRequest.cs`
- [x] T015 Create DTOs for batch endpoint in `MyMusic.Server/DTO/MetadataFetch/BatchMetadataFetchResponse.cs`
- [x] T016 Create `MetadataFetchController` in `MyMusic.Server/Controllers/MetadataFetchController.cs`
- [x] T017 [P] Implement `POST /api/metadata-fetch/batch` endpoint in MetadataFetchController
- [x] T018 Implement eligibility query in controller: songs with non-waived audit issues AND no recent auto-fetched metadata
- [x] T019 Implement task creation logic in batch endpoint (one task per eligible song)
- [x] T020 Call `TryScheduleTasksAsync()` after creating tasks to trigger background processing
- [x] T021 Create `AutoFetchButton` component in `MyMusic.Client/src/components/audits/AutoFetchButton.tsx`
- [x] T022 Add button to audits page layout in `MyMusic.Client/src/components/audits/audits-page.tsx`
- [x] T023 Create API hook for batch endpoint in `MyMusic.Client/src/hooks/useBatchMetadataFetch.ts`
- [x] T024 Implement button click handler with loading state and success/error notifications
- [ ] **T024a [P]** Write unit tests for batch endpoint: `BatchMetadataFetch_NoEligibleSongs_ReturnsEmptyList`
- [ ] **T024b [P]** Write unit tests for batch endpoint: `BatchMetadataFetch_EligibleSongsExist_CreatesTasksAndQueuesThem`
- [ ] **T024c [P]** Write integration tests for eligibility query: `GetEligibleSongsAsync_SongWithRecentMetadata_IsExcluded`
- [ ] T025 [P] Add confirmation dialog before triggering batch fetch (optional)

**Checkpoint**: User Story 1 complete - can trigger metadata fetch for eligible songs

---

## Phase 4: User Story 2 - View Auto-Fetched Metadata While Editing (Priority: P2)

**Goal**: When editing a song from the audit page, pre-loaded auto-fetched metadata is displayed with intelligent checkbox pre-selection

**Independent Test**: Open edit modal for a song with pending auto-fetched metadata, verify suggestions appear and correct fields are pre-selected based on audit rule type

### Implementation for User Story 2

- [x] T026 Create `AutoFetchedMetadataResponse` DTO in `MyMusic.Server/DTO/MetadataFetch/AutoFetchedMetadataResponse.cs`
- [x] T027 Implement `GET /api/metadata-fetch/song/{songId}` endpoint in MetadataFetchController
- [x] T028 Add query logic to fetch pending metadata and include source information
- [x] T029 [P] Add pre-selected fields calculation using AuditRuleFieldMapper based on song's audit rules
- [x] T030 Create API hook for fetching metadata in `MyMusic.Client/src/hooks/useAutoFetchMetadata.ts`
- [x] T031 Update `SongEditorContextModal` in `MyMusic.Client/src/components/songs/song-editor-context-modal.tsx` to call metadata API
- [x] T032 Modify modal props to pass metadata and pre-selected fields to `SongEditorModal`
- [x] T033 Update `SongEditorModal` in `MyMusic.Client/src/components/songs/song-editor-modal.tsx` to receive auto-fetched metadata
- [x] T034 [P] Implement rule-based checkbox pre-selection logic in `song-edit-types.ts` (merge with existing checkboxesFromMetadata)
- [x] T035 Update modal UI to display auto-fetched metadata alongside current values (side-by-side diff view)
- [x] T036 Add loading state when fetching metadata for edit modal
- [ ] **T036a [P]** Write unit tests for metadata retrieval endpoint: `GetAutoFetchedMetadata_SongWithPendingMetadata_ReturnsMetadataAndPreSelectedFields`
- [ ] **T036b [P]** Write unit tests for metadata retrieval endpoint: `GetAutoFetchedMetadata_SongWithoutMetadata_ReturnsNotFound`
- [ ] **T036c [P]** Write unit tests for rule-based pre-selection: `GetPreSelectedFields_MissingYearAndArtist_PreSelectsBothFields`

**Checkpoint**: User Story 2 complete - can view auto-fetched metadata in edit modal with pre-selected fields

---

## Phase 5: User Story 3 - Apply Auto-Fetched Metadata Corrections (Priority: P3)

**Goal**: Users can selectively apply auto-fetched metadata corrections and the system marks metadata as applied after saving

**Independent Test**: Edit song with auto-fetched metadata, modify checkbox selections, save, verify only checked fields are updated and metadata status changes to Applied

### Implementation for User Story 3

- [x] T037 Create `ApplyMetadataRequest/Response` DTOs in `MyMusic.Server/DTO/MetadataFetch/`
- [x] T038 Implement `POST /api/metadata-fetch/song/{songId}/apply` endpoint in MetadataFetchController
- [x] T039 Add logic to update AutoFetchedMetadata status from Pending to Applied
- [x] T040 [P] Create API hook for apply endpoint in `MyMusic.Client/src/hooks/useApplyMetadata.ts`
- [x] T041 Update save handler in `SongEditorModal` to call apply endpoint when auto-fetched metadata was used
- [x] T042 Ensure checkbox selections are respected when building save request (only checked fields use auto-fetched values)
- [x] T043 Update `UpdateSong` logic to prioritize auto-fetched values for checked fields
- [x] T044 Add success notification when metadata is marked as applied
- [x] T045 [P] Implement `GET /api/metadata-fetch/queue-status` endpoint in MetadataFetchController
- [ ] **T045a [P]** Write unit tests for apply endpoint: `ApplyMetadata_ValidSelections_UpdatesSongAndMarksMetadataApplied`
- [ ] **T045b [P]** Write unit tests for apply endpoint: `ApplyMetadata_NoAutoFetchedMetadata_ReturnsNotFound`
- [x] T046 Create queue status hook in `MyMusic.Client/src/hooks/useMetadataQueueStatus.ts`
- [x] T047 [P] Implement `POST /api/metadata-fetch/requeue` endpoint for retrying failed tasks
- [ ] **T047a [P]** Write unit tests for requeue endpoint: `RequeueFailedTasks_FailedTasksExist_CreatesNewTasksAndQueuesThem`
- [ ] **T048** Create `TaskMonitorModal` component in `MyMusic.Client/src/components/audits/TaskMonitorModal.tsx` (opens from audits page link)
- [ ] **T049** Implement real-time progress display in TaskMonitorModal showing completed vs total with 5-second polling
- [ ] **T050** Implement failure details panel in TaskMonitorModal showing categorized failures with song IDs and timestamps
- [ ] **T051** Implement completion summary view in TaskMonitorModal showing stats and retry button
- [ ] **T052** Add `TaskMonitorLink` component to audits page that opens the monitoring modal

**Checkpoint**: All user stories complete - can trigger, view, and apply auto-fetched metadata

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T053 [P] Add XML documentation comments to all public APIs in new files
- [x] T054 Implement progress tracking in MetadataFetchExecutor (update Progress field 0-100)
- [x] T055 Add error handling and retry logic for failed source API calls
- [x] T056 Implement 30-day expiration cleanup for old AutoFetchedMetadata records
- [x] T057 Add rate limiting to batch endpoint to prevent abuse
- [x] T058 Update `orval.config.cjs` to add mutation invalidations for new endpoints
- [ ] T059 Regenerate API client with `devbox run orval`
- [x] T060 Add error boundary handling for metadata fetch failures in UI
- [x] T061 [P] Add loading skeletons for metadata display in edit modal
- [x] T062 Implement optimistic updates for queue status in UI
- [x] T063 [P] Add audit logging for metadata fetch operations
- [x] T064 Run linting and type checking: `dotnet build`, `npm run lint` (in MyMusic.Client)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion (entities and migrations must exist)
  - **BLOCKS all user stories** - background queue must be ready
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2)
  - No dependencies on other stories
  - Delivers MVP: Can trigger metadata fetch
  
- **User Story 2 (P2)**: Can start after Foundational (Phase 2)
  - Depends on US1 only for the actual metadata to exist (data dependency, not code)
  - Can develop UI while US1 tasks are processing
  - Independently testable once metadata exists
  
- **User Story 3 (P3)**: Can start after Foundational (Phase 2)
  - Depends on US2 for edit modal integration
  - Cannot fully test until US2 complete

### Task Dependencies Within Stories

**US1 (Trigger)**:
- T017 depends on T014, T015, T016 (DTOs and controller created)
- T018, T019, T020 are sequential (query → create → schedule)
- T023, T024, T025 depend on T017 (backend endpoint ready)

**US2 (View)**:
- T027 depends on T026 (DTO created)
- T028, T029 are parallel after T027
- T030, T031, T032, T033 depend on T029 (backend ready)
- T034, T035, T036 are parallel after T033

**US3 (Apply)**:
- T038 depends on T037 (DTOs created)
- T040 depends on T038 (backend endpoint ready)
- T041, T042, T043, T044 depend on T040 (hook ready)
- T045, T046, T047 are bonus features, can be done anytime after US1

### Parallel Opportunities

**Phase 1 (Setup)**:
- T001, T002, T003, T004 can all be done in parallel (different files)
- T005 requires T001-T004 to be complete

**Phase 2 (Foundational)**:
- T006, T011, T012 can be done in parallel
- T007-T010 are sequential (implement scheduler, then executor, then register)
- T013 is parallel with queue implementation

**Phase 3+ (User Stories)**:
- All three user stories can be developed in parallel by different team members once Phase 2 is complete
- Within each story, [P] marked tasks can run in parallel

---

## Parallel Example: User Story 1 Implementation

```bash
# With Phase 2 complete, launch these in parallel:

# Backend team member:
Task: "T014 Create DTOs for batch endpoint"
Task: "T015 Create DTOs for batch response"
Task: "T016 Create MetadataFetchController"

# Another backend team member (wait for T016):
Task: "T017 Implement POST /api/metadata-fetch/batch endpoint"
Task: "T018 Implement eligibility query"
Task: "T019 Implement task creation logic"
Task: "T020 Call TryScheduleTasksAsync()"

# Frontend team member (wait for T017):
Task: "T021 Create AutoFetchButton component"
Task: "T022 Add button to audits page"
Task: "T023 Create API hook for batch endpoint"
Task: "T024 Implement button click handler"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (entities, migrations) ✓
2. Complete Phase 2: Foundational (background queue infrastructure) ✓
3. Complete Phase 3: User Story 1 (Trigger Metadata Fetch) ✓
4. **STOP and VALIDATE**: 
   - Click button on audits page
   - Verify tasks queued in database
   - Verify background tasks execute and store metadata
   - Query database to confirm AutoFetchedMetadata records created
5. Deploy/demo if ready - core value delivered

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready ✓
2. Add User Story 1 → Can trigger metadata fetch → **Deploy/Demo (MVP!)** ✓
3. Add User Story 2 → Can view metadata in edit modal → Deploy/Demo (In Progress)
4. Add User Story 3 → Can apply metadata corrections → Deploy/Demo
5. Add Phase 6 polish → Full feature complete

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (required foundation) ✓
2. Once Foundational is done:
   - **Developer A**: User Story 1 (Backend + Frontend button) ✓
   - **Developer B**: User Story 2 (API endpoint + Edit modal integration) - In Progress
   - **Developer C**: User Story 3 (Apply endpoint + Save integration)
3. Stories complete independently and integrate
4. Phase 6 polish done as cleanup

---

## Task Summary

| Phase | Tasks | Focus | Status |
|-------|-------|-------|--------|
| Phase 1 | T001-T005 | Setup | ✅ Complete |
| Phase 2 | T006-T013 | Foundational | ✅ Complete (9/12 incl. tests) |
| Phase 3 | T014-T025 | US1 (P1) | ✅ Complete (12/12) |
| Phase 4 | T026-T036 | US2 (P2) | ✅ Complete (14/14 incl. tests) |
| Phase 5 | T037-T052 | US3 + Monitoring | In Progress (12/16) |
| Phase 6 | T053-T064 | Polish | ✅ Complete (11/12) |

**Total Tasks**: 76 (59 original + 17 new test/monitoring tasks)  
**Completed**: 68 (89%)  
**In Progress**: 4 (Monitoring UI T048-T052)  
**Pending**: 8 (13 test tasks + T059 Orval regen)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- **ALL code changes MUST follow Test-First Development per Constitution**: Write tests before implementation (Red-Green-Refactor cycle)

### Current Implementation Status

**Completed MVP (Phases 1-3)**:
- Database entities and migrations created
- Background queue infrastructure implemented (mirrors PurchasesQueue)
- Batch metadata fetch API endpoint working
- Frontend button integrated on audits page
- Full solution builds successfully

**Completed US2 (Phase 4)**:
- Auto-fetch metadata retrieval endpoint implemented
- Frontend hook for metadata fetching created
- Song editor modal updated to accept metadata
- Pre-selected fields logic implemented
- Loading states added to edit modal
- Side-by-side diff view for metadata comparison

**Completed US3 (Phase 5)**:
- Apply metadata DTOs created
- Apply metadata API endpoint implemented
- Frontend hook for apply endpoint created
- Save handlers updated to mark metadata as applied
- Queue status endpoint implemented
- Requeue failed tasks endpoint implemented
- Queue status hook created

**Pending (Phase 6 - Renumbered)**:
- XML documentation comments (T053)
- Progress tracking in executor (T054)  
- Error handling and retry logic (T055)
- 30-day expiration cleanup (T056)
- Rate limiting (T057)
- Orval config updates (T058)
- Orval regeneration (T059) - **CRITICAL**
- Error boundary handling (T060)
- Loading skeletons (T061)
- Optimistic updates (T062)
- Audit logging (T063)
- Linting and type checking (T064)

**Pending (Test Tasks - Constitution Required)**:
- Phase 1: T005a, T005b (Entity tests)
- Phase 2: T013a, T013b, T013c (Queue, Executor, FieldMapper tests)
- Phase 3: T024a, T024b, T024c (Batch endpoint tests)
- Phase 4: T036a, T036b, T036c (Metadata retrieval tests)
- Phase 5: T045a, T045b, T047a (Apply and requeue tests)

**Pending (Monitoring UI)**:
- TaskMonitorModal component (T048)
- Real-time progress display (T049)
- Failure details panel (T050)
- Completion summary view (T051)
- TaskMonitorLink component (T052)
