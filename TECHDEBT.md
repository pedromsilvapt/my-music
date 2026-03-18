# Technical Debt Tracking

This document tracks all identified technical debt in the MyMusic codebase. Each item is an independent task that can be completed separately.

## Important Rules (MUST FOLLOW)

### Mandatory Testing Rule

**ALL technical debt tasks MUST include test creation BEFORE implementing changes**, with the following exceptions:

- Typo fixes in comments or strings
- Comment additions or updates
- Whitespace/formatting changes
- File renames without logic changes
- Simple code moves without behavior changes

**For all other changes:**
1. Write tests that verify the CURRENT behavior before any changes
2. Run tests to ensure they pass (documenting the existing state)
3. Implement the refactoring
4. Run tests again to verify nothing broke
5. If tests cannot be written beforehand, **ASK USER PERMISSION** to skip this requirement

**Why this rule exists:** Technical debt solutions should NEVER change functionality. Tests ensure we maintain the same behavior while improving code quality.

---

## 1. Backend Services - Duplicated Code

### TD0001 - Create `AuditRuleBase<TCriterion>` base class
**File(s):** `MyMusic.Common/Services/AuditRules/MissingCoverAuditRule.cs:13-38`, `MissingGenresAuditRule.cs:13-38`, `MissingLyricsAuditRule.cs:13-38`, `MissingYearAuditRule.cs:13-38`, `NonSquareCoverAuditRule.cs:13-42`, `NonJpegCoverAuditRule.cs:13-42`, `SmallCoverAuditRule.cs:16-47`, `MediumCoverAuditRule.cs:16-51`  
**Severity:** High  
**Impact:** High  
**Description:** All 8 audit rules share identical 4-step scan patterns. Extract base class to encapsulate common scanning logic, requiring only the filtering predicate from each implementation.  
**Effort:** Medium  
**Test Strategy:** Write tests for each audit rule's current behavior, then refactor to use base class ensuring same results.  
- [ ] TD0001

### TD0002 - Consolidate MetadataDiffBuilder sync/async methods
**File(s):** `MyMusic.Common/Services/MetadataDiffBuilder.cs:21-153 (CreateDiffAsync)`, `MyMusic.Common/Services/MetadataDiffBuilder.cs:158-270 (CreateDiff)`  
**Severity:** Medium  
**Impact:** Medium  
**Description:** `CreateDiffAsync()` and `CreateDiff()` are nearly identical. Sync version should call async version with synchronous image comparison, or extract shared logic into private method.  
**Effort:** Small  
**Test Strategy:** Create tests for both methods with same inputs, verify identical outputs, then consolidate.  
- [ ] TD0002

### TD0003 - Extract shared background service patterns from PurchasesQueue and MetadataFetchQueue
**File(s):** `MyMusic.Common/Services/PurchasesQueue.cs:27-103`, `MyMusic.Common/Services/MetadataFetchQueue.cs:32-137`  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Both Scheduler inner classes have nearly identical patterns for PullNextTasksAsync, SetTaskRunningAsync, SetTaskFailedAsync, SetTaskFinishedAsync, UpdateTaskStore. Create more comprehensive base class.  
**Effort:** Medium  
**Test Strategy:** Write integration tests for queue operations before refactoring.  
- [ ] TD0003

### TD0004 - Create shared EntityResolutionService for GetOrCreate patterns
**File(s):** `MyMusic.Common/Services/SongUpdateService.cs:180-243, 330-373`, `MyMusic.Common/Services/MusicService.cs` (similar patterns)  
**Severity:** High  
**Impact:** High  
**Description:** `GetOrCreateArtistAsync()` and `GetOrCreateGenreAsync()` patterns appear in both SongUpdateService and likely other services. Extract to shared EntityResolutionService.  
**Effort:** Medium  
**Test Strategy:** Test entity resolution behavior before and after extraction.  
- [ ] TD0004

### TD0005 - Create Base64UrlEncoder utility class
**File(s):** `MyMusic.Common/Services/ThumbnailProxyService.cs:166-189`  
**Severity:** Low  
**Impact:** Low  
**Description:** `EncodeBase64Url` and `DecodeBase64Url` methods are in service class but could be shared utilities. Move to `MyMusic.Common/Utilities/Base64UrlEncoder.cs`.  
**Effort:** Small  
**Test Strategy:** Write unit tests for encoding/decoding round-trip.  
- [ ] TD0005

### TD0006 - Create shared test database setup utility
**File(s):** `MyMusic.Common.Tests/Scenario.cs:50-64`, `MyMusic.Common.Tests/Filters/DynamicFilterBuilderSpecs.cs:24-33`  
**Severity:** Medium  
**Impact:** Medium  
**Description:** In-memory SQLite database setup is duplicated across test files. Create shared `TestDbContextFactory` in `MyMusic.Common.Tests/Utilities/`.  
**Effort:** Small  
**Test Strategy:** This is test infrastructure - ensure existing tests pass after extraction.  
- [ ] TD0006

### TD0007 - Create shared transaction helper for services
**File(s):** `MyMusic.Common/Services/MusicService.cs:269, 651, 658, 670` (and other services)  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Transaction handling is duplicated across services. Create shared pattern or extension method for common transaction operations.  
**Effort:** Small  
**Test Strategy:** Write tests verifying transaction behavior before creating helper.  
- [ ] TD0007

### TD0008 - Consolidate error categorization strings in MetadataFetchQueue
**File(s):** `MyMusic.Common/Services/MetadataFetchQueue.cs:92-104`  
**Severity:** Low  
**Impact:** Low  
**Description:** String literals like "timeout", "service unavailable" used for error categorization should be constants or enums.  
**Effort:** Small  
**Test Strategy:** Ensure categorization logic tests pass after string-to-constant conversion.  
- [ ] TD0008

---

## 2. Backend Services - Single Responsibility Violations

### TD0009 - Split MusicService: Extract DeviceService
**File(s):** `MyMusic.Common/Services/MusicService.cs:35-135` (Device Management)  
**Severity:** High  
**Impact:** High  
**Description:** Device CRUD operations in MusicService should be in separate DeviceService.  
**Effort:** Medium  
**Test Strategy:** Write tests for device operations before extraction, ensure they pass after move.  
- [ ] TD0009

### TD0010 - Split MusicService: Extract SongImportService
**File(s):** `MyMusic.Common/Services/MusicService.cs:232-676` (Song Import/Repository Management)  
**Severity:** High  
**Impact:** High  
**Description:** Complex 400+ line import logic should be in dedicated SongImportService.  
**Effort:** Large  
**Test Strategy:** Comprehensive tests for import scenarios before refactoring.  
- [ ] TD0010

### TD0011 - Split MusicService: Extract FileSystemService
**File(s):** `MyMusic.Common/Services/MusicService.cs:173-219, 280-381` (File System Operations)  
**Severity:** Medium  
**Impact:** High  
**Description:** Directory scanning, file extension checking, path manipulation should be in FileSystemService.  
**Effort:** Medium  
**Test Strategy:** Mock file system tests before extraction.  
- [ ] TD0011

### TD0012 - Split MusicService: Extract ChecksumService
**File(s):** `MyMusic.Common/Services/MusicService.cs:678-701` (Checksum Calculation)  
**Severity:** Low  
**Impact:** Low  
**Description:** Static checksum methods should be in dedicated ChecksumService.  
**Effort:** Small  
**Test Strategy:** Test checksum calculations before move.  
- [ ] TD0012

### TD0013 - Split SongUpdateService: Extract EntityResolutionService
**File(s):** `MyMusic.Common/Services/SongUpdateService.cs:180-243, 330-373` (Entity Creation Logic)  
**Severity:** Medium  
**Impact:** Medium  
**Description:** GetOrCreateArtistAsync and GetOrCreateGenreAsync should be in EntityResolutionService.  
**Effort:** Medium  
**Test Strategy:** Test entity resolution before extraction.  
- [ ] TD0013

### TD0014 - Split SongUpdateService: Extract FileMetadataService
**File(s):** `MyMusic.Common/Services/SongUpdateService.cs:375-396` (File System Operations)  
**Severity:** Medium  
**Impact:** Medium  
**Description:** File I/O and metadata writing should be in FileMetadataService.  
**Effort:** Small  
**Test Strategy:** Test file operations before extraction.  
- [ ] TD0014

### TD0015 - Split SongUpdateService: Extract SongLabelBuilder
**File(s):** `MyMusic.Common/Services/SongUpdateService.cs:398-403` (Label Building)  
**Severity:** Low  
**Impact:** Low  
**Description:** String formatting for labels should be in dedicated builder class.  
**Effort:** Small  
**Test Strategy:** Test label generation before extraction.  
- [ ] TD0015

### TD0016 - Split MetadataFetchQueue: Extract MetadataFetchExecutor
**File(s):** `MyMusic.Common/Services/MetadataFetchQueue.cs:142-421` (Executor inner class)  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Move MetadataFetchExecutor to its own file/class.  
**Effort:** Medium  
**Test Strategy:** Test executor behavior before extraction.  
- [ ] TD0016

### TD0017 - Split MetadataFetchQueue: Extract SourceSongMatcher
**File(s):** `MyMusic.Common/Services/MetadataFetchQueue.cs:366-420` (Best match scoring algorithm)  
**Severity:** Low  
**Impact:** Medium  
**Description:** Scoring algorithm should be in dedicated SourceSongMatcher class.  
**Effort:** Small  
**Test Strategy:** Test scoring logic before extraction.  
- [ ] TD0017

### TD0018 - Split MetadataFetchQueue: Extract MetadataPersistenceService
**File(s):** `MyMusic.Common/Services/MetadataFetchQueue.cs:231-277` (Database persistence)  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Database operations should be in dedicated persistence service.  
**Effort:** Small  
**Test Strategy:** Test persistence operations before extraction.  
- [ ] TD0018

### TD0019 - Split ThumbnailProxyService: Extract UrlSafeBase64Codec
**File(s):** `MyMusic.Common/Services/ThumbnailProxyService.cs:166-190` (URL Encoding/Decoding)  
**Severity:** Low  
**Impact:** Low  
**Description:** Base64 URL-safe encoding should be in separate codec class.  
**Effort:** Small  
**Test Strategy:** Already covered by TD0005.  
- [ ] TD0019

### TD0020 - Split ThumbnailProxyService: Extract ImageCacheService
**File(s):** `MyMusic.Common/Services/ThumbnailProxyService.cs:130-163` (Caching Logic)  
**Severity:** Low  
**Impact:** Low  
**Description:** Distributed cache operations should be in ImageCacheService.  
**Effort:** Small  
**Test Strategy:** Test caching behavior before extraction.  
- [ ] TD0020

### TD0021 - Move SongsController business logic to MetadataMatchingService
**File(s):** `MyMusic.Server/Controllers/SongsController.cs:1072-1114` (FindClosestMatch, CalculateMatchScore)  
**Severity:** High  
**Impact:** High  
**Description:** Business logic for metadata matching should be in service layer, not controller.  
**Effort:** Medium  
**Test Strategy:** Test matching algorithm before move to service.  
- [ ] TD0021

---

## 3. Backend Services - Code Consistency

### TD0022 - Standardize logger field naming to _logger
**File(s):** `MyMusic.Common/Services/MusicService.cs:30`, `MyMusic.Common/Services/SongUpdateService.cs:26`, `MyMusic.Common/Services/ThumbnailProxyService.cs:14,17`, and others  
**Severity:** Low  
**Impact:** Low  
**Description:** Use private readonly `_logger` field consistently across all services.  
**Effort:** Small  
**Test Strategy:** No functional change - compilation check sufficient.  
- [ ] TD0022

### TD0023 - Remove public settable logger properties
**File(s):** `MyMusic.Common/Services/MusicService.cs:30` - `public ILogger Logger { get; set; }`  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Public mutable properties for Logger, FileSystem, Config should be private readonly fields.  
**Effort:** Small  
**Test Strategy:** Verify no external code sets these properties.  
- [ ] TD0023

### TD0024 - Standardize exception types for domain errors
**File(s):** `MyMusic.Common/Services/MusicService.cs:52, 83`, `MyMusic.Common/Services/SourcesService.cs:14`  
**Severity:** Medium  
**Impact:** High  
**Description:** Some services throw generic Exception, others throw custom domain exceptions (SourceIdNotFoundException). Standardize on custom domain exceptions.  
**Effort:** Medium  
**Test Strategy:** Write tests for exception scenarios before standardizing.  
- [ ] TD0024

### TD0025 - Document DbContext injection patterns
**File(s):** Various services use different patterns  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Create documentation for when to use constructor injection vs method parameter injection vs controller injection.  
**Effort:** Small  
**Test Strategy:** Documentation only - no code changes.  
- [ ] TD0025

### TD0026 - Standardize primary constructor usage
**File(s):** Multiple files with different patterns  
**Severity:** Low  
**Impact:** Low  
**Description:** Use primary constructor + private readonly fields consistently. Avoid public settable properties.  
**Effort:** Small  
**Test Strategy:** No functional change - compilation check sufficient.  
- [ ] TD0026

### TD0027 - Remove commented-out code blocks
**File(s):** `MyMusic.Common/Services/MusicService.cs:611-618`  
**Severity:** Low  
**Impact:** Low  
**Description:** Remove large block of commented code about Overwrite strategy.  
**Effort:** Small  
**Test Strategy:** No functional change - can skip tests.  
- [ ] TD0027

### TD0028 - Extract magic numbers to constants in MetadataDiffBuilder
**File(s):** `MyMusic.Common/Services/MetadataDiffBuilder.cs:374 (30), 386 (100), 391 (50), 407 (5)`  
**Severity:** Low  
**Impact:** Low  
**Description:** Artist match points, title match points, duration threshold should be named constants.  
**Effort:** Small  
**Test Strategy:** Test scoring still works with same values as constants.  
- [ ] TD0028

### TD0029 - Document AsyncReaderWriterLock usage guidelines
**File(s):** `MyMusic.Common/Services/MusicService.cs:28`  
**Severity:** Low  
**Impact:** Low  
**Description:** Document when to use locking or consider extracting RepositoryLockService.  
**Effort:** Small  
**Test Strategy:** Documentation only.  
- [ ] TD0029

### TD0030 - Standardize repository/database access patterns
**File(s):** `MyMusic.Common/Services/MusicService.cs` (mixed patterns)  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Some methods use UserMusicService helper, others use direct DbContext queries. Standardize on one approach.  
**Effort:** Medium  
**Test Strategy:** Test all database access patterns before standardizing.  
- [ ] TD0030

---

## 4. DTOs - Consistency Issues

### TD0031 - Add XML documentation to all DTOs
**File(s):** All files in `MyMusic.Server/DTO/` except MetadataFetch (88 files)  
**Severity:** Medium  
**Impact:** Low  
**Description:** Follow MetadataFetch pattern and add XML documentation to all public DTOs.  
**Effort:** Large  
**Test Strategy:** Documentation only - can skip tests.  
- [ ] TD0031

### TD0032 - Standardize Item class naming (singular vs plural)
**File(s):** `MyMusic.Server/DTO/Songs/ListSongsResponse.cs` (ListSongsItem), `MyMusic.Server/DTO/Playlists/ListPlaylistsResponse.cs` (ListPlaylistItem), etc.  
**Severity:** Low  
**Impact:** Low  
**Description:** Most use singular (PlaylistItem), but Songs/Albums/Artists use plural (SongsItem). Standardize on singular.  
**Effort:** Medium  
**Test Strategy:** Search/replace in DTOs and their usages - ensure compilation.  
- [ ] TD0032

### TD0033 - Standardize duration formatting across DTOs
**File(s):** `MyMusic.Server/DTO/Songs/ListSongsItem.cs:40` (interpolation), `MyMusic.Server/DTO/Albums/GetAlbumResponse.cs:63` (ToString)  
**Severity:** Low  
**Impact:** Medium  
**Description:** Choose one duration format pattern and apply consistently.  
**Effort:** Small  
**Test Strategy:** Verify duration strings match expected format in tests.  
- [ ] TD0033

### TD0034 - Create shared ArtistDto type
**File(s):** Multiple Artist DTOs across resources: `ListSongsArtist`, `GetSongResponseArtist`, `GetAlbumResponseArtist`, etc.  
**Severity:** Medium  
**Impact:** High  
**Description:** Create shared ArtistDto in common location to reduce 7+ nearly-identical types.  
**Effort:** Medium  
**Test Strategy:** Test serialization/deserialization before and after consolidation.  
- [ ] TD0034

### TD0035 - Create shared AlbumDto type
**File(s):** Multiple Album DTOs across resources: `ListSongsAlbum`, `GetSongResponseAlbum`, `GetAlbumResponseSongAlbum`, etc.  
**Severity:** Medium  
**Impact:** High  
**Description:** Create shared AlbumDto in common location to reduce 7+ similar types.  
**Effort:** Medium  
**Test Strategy:** Test serialization/deserialization before and after consolidation.  
- [ ] TD0035

### TD0036 - Create shared GenreDto type
**File(s):** Multiple Genre DTOs across resources: `ListSongsGenre`, `GetSongResponseGenre`, `GetAlbumResponseGenre`, etc.  
**Severity:** Low  
**Impact:** Medium  
**Description:** Create shared GenreDto in common location to reduce 5+ similar types.  
**Effort:** Small  
**Test Strategy:** Test serialization/deserialization before and after consolidation.  
- [ ] TD0036

### TD0037 - Standardize mapping patterns
**File(s):** Various DTOs - some use AgileMapper, some manual mapping  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Choose one approach (Manual or AgileMapper) and apply consistently across all DTOs.  
**Effort:** Large  
**Test Strategy:** Comprehensive mapping tests before standardizing.  
- [ ] TD0037

### TD0038 - Standardize property modifiers to init for response DTOs
**File(s):** Multiple response DTOs with mixed `init` and `set`  
**Severity:** Low  
**Impact:** Medium  
**Description:** Use `init` for response DTOs to enforce immutability after construction.  
**Effort:** Medium  
**Test Strategy:** Verify DTO creation still works after changing set to init.  
- [ ] TD0038

### TD0039 - Add missing FromEntity methods to DTOs
**File(s):** `UpdateSongItem`, `FetchMetadataResponse`, `ToggleFavoriteResponse`, etc.  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Add FromEntity methods to DTOs that currently lack them for consistent mapping pattern.  
**Effort:** Medium  
**Test Strategy:** Test mapping before adding FromEntity methods.  
- [ ] TD0039

### TD0040 - Create Shared.cs for Songs, Albums, Artists resources
**File(s):** Currently only Sync, Sources, Purchases have Shared.cs  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Other resources like Songs, Albums, Artists duplicate similar types instead of sharing them.  
**Effort:** Medium  
**Test Strategy:** Test existing functionality before extraction.  
- [ ] TD0040

### TD0041 - Standardize nested Item class naming
**File(s):** `GetSongResponseSong`, `GetAlbumResponseSong`, `GetPlaylistSong` (inconsistent!)  
**Severity:** Low  
**Impact:** Low  
**Description:** Standardize on consistent naming pattern for nested song types.  
**Effort:** Small  
**Test Strategy:** Search/replace to consistent naming.  
- [ ] TD0041

---

## 5. Frontend - Pattern Violations

### TD0042 - Fix useShallow violation in collection.tsx
**File(s):** `MyMusic.Client/src/components/common/collection/collection.tsx:70-79`  
**Severity:** High  
**Impact:** High  
**Description:** Selector returns new object without useShallow, causing unnecessary re-renders. Wrap with useShallow.  
**Effort:** Small  
**Test Strategy:** Test collection component behavior before and after fix.  
- [ ] TD0042

### TD0043 - Regenerate Orval client for metadata-fetch endpoints
**File(s):** All metadata-fetch hooks using manual fetch  
**Severity:** High  
**Impact:** High  
**Description:** Manual fetch calls suggest APIs not in OpenAPI spec or client not regenerated. Regenerate Orval client.  
**Effort:** Small  
**Test Strategy:** Ensure generated client works with existing tests.  
- [ ] TD0043

### TD0044 - Convert manual fetch calls to use generated clients
**File(s):** `songs-page.tsx:35-36`, `playlists-page.tsx:30-31`, `useApplyMetadata.ts:16-28`, `useBatchMetadataFetch.ts:18-32`, and others (11+ files)  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Replace manual fetch with Orval-generated client functions.  
**Effort:** Medium  
**Test Strategy:** Test all affected components/hooks after conversion.  
- [ ] TD0044

### TD0045 - Standardize error handling pattern
**File(s):** Various hooks and components with different error handling approaches  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Three different approaches found (useQueryData, manual checks, throwing errors). Choose one standard.  
**Effort:** Medium  
**Test Strategy:** Test error scenarios before standardizing.  
- [ ] TD0045

### TD0046 - Document context usage guidelines
**File(s):** `manage-playlists-context.tsx`, `manage-devices-context.tsx`, `artwork-lightbox-context.tsx` vs `player-context.tsx`, `collection-context.tsx`  
**Severity:** Low  
**Impact:** Medium  
**Description:** Document when to use React Context vs Zustand stores for state management.  
**Effort:** Small  
**Test Strategy:** Documentation only.  
- [ ] TD0046

### TD0047 - Remove commented code in __root.tsx
**File(s):** `MyMusic.Client/src/routes/__root.tsx:8-16`  
**Severity:** Low  
**Impact:** Low  
**Description:** Remove commented-out code that should be deleted.  
**Effort:** Small  
**Test Strategy:** No functional change - can skip tests.  
- [ ] TD0047

### TD0048 - Audit all Zustand store usage for useShallow compliance
**File(s):** All frontend components using Zustand stores  
**Severity:** Medium  
**Impact:** High  
**Description:** Systematic review of all usePlayerContext, useCollectionActions, etc. to ensure useShallow compliance.  
**Effort:** Medium  
**Test Strategy:** Visual regression testing to catch re-render issues.  
- [ ] TD0048

### TD0049 - Document when to use manual fetch vs generated clients
**File(s):** Documentation only  
**Severity:** Low  
**Impact:** Medium  
**Description:** Document exceptions where manual fetch is necessary vs using generated clients.  
**Effort:** Small  
**Test Strategy:** Documentation only.  
- [ ] TD0049

### TD0050 - Review and fix any other useShallow violations
**File(s):** TBD after TD0048 audit  
**Severity:** Medium  
**Impact:** High  
**Description:** Fix any additional useShallow violations found during audit.  
**Effort:** Varies  
**Test Strategy:** Test affected components after fixes.  
- [ ] TD0050

---

## 6. Cross-Project - Shared Utilities

### TD0051 - Standardize IFileSystem service registration
**File(s):** `MyMusic.Common/HostBuilderExtensions.cs`, `MyMusic.Server/HostBuilderExtensions.cs:36`  
**Severity:** High  
**Impact:** High  
**Description:** IFileSystem registered as Scoped in Common but Singleton in Server. Standardize on Scoped.  
**Effort:** Small  
**Test Strategy:** Ensure file operations still work after registration change.  
- [ ] TD0051

### TD0052 - Change IApiPathResolver to Scoped registration
**File(s):** `MyMusic.Server/HostBuilderExtensions.cs:78`  
**Severity:** Medium  
**Impact:** Medium  
**Description:** IApiPathResolver is Singleton but depends on reloadable IOptions. Change to Scoped.  
**Effort:** Small  
**Test Strategy:** Verify path resolution still works after change.  
- [ ] TD0052

### TD0053 - Fix ThumbnailCacheConfig registration to use using directive
**File(s):** `MyMusic.Server/HostBuilderExtensions.cs:75`  
**Severity:** Low  
**Impact:** Low  
**Description:** Using fully qualified name instead of using directive. Make consistent with other registrations.  
**Effort:** Small  
**Test Strategy:** No functional change - compilation check sufficient.  
- [ ] TD0053

### TD0054 - Add logging to SourcesService
**File(s):** `MyMusic.Common/Services/SourcesService.cs`  
**Severity:** Low  
**Impact:** Low  
**Description:** This service has no logging while others log extensively. Add appropriate logging.  
**Effort:** Small  
**Test Strategy:** Add logging - verify it works, no functional changes.  
- [ ] TD0054

### TD0055 - Standardize configuration class structure
**File(s):** `MyMusic.Common/Config.cs`, `MyMusic.Server/ServerConfig.cs`, `MyMusic.CLI/Configuration/MyMusicOptions.cs`  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Configuration is fragmented across multiple classes. Standardize structure and consolidate where possible.  
**Effort:** Medium  
**Test Strategy:** Test configuration loading before and after standardization.  
- [ ] TD0055

### TD0056 - Standardize CLI logging approach
**File(s):** `MyMusic.CLI/Program.cs:121-176`  
**Severity:** Low  
**Impact:** Low  
**Description:** CLI implements custom FileLoggerProvider. Consider standard logging package (Serilog/NLog).  
**Effort:** Medium  
**Test Strategy:** Test CLI logging before and after standardization.  
- [ ] TD0056

---

## 7. Testing - Pattern Inconsistencies

### TD0057 - Refactor DynamicFilterBuilderSpecs to use Scenario class
**File(s):** `MyMusic.Common.Tests/Filters/DynamicFilterBuilderSpecs.cs:24-33`  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Duplicates database setup logic instead of using Scenario.CreateDbContext().  
**Effort:** Small  
**Test Strategy:** Ensure tests pass after using Scenario.  
- [ ] TD0057

### TD0058 - Create shared TestDbContextFactory utility
**File(s):** Create in `MyMusic.Common.Tests/Utilities/`  
**Severity:** Medium  
**Impact:** Medium  
**Description:** Shared utility for in-memory SQLite database setup across all test files.  
**Effort:** Small  
**Test Strategy:** This is test infrastructure - ensure existing tests pass.  
- [ ] TD0058

### TD0059 - Document test assertion style guidelines
**File(s):** Documentation only  
**Severity:** Low  
**Impact:** Low  
**Description:** Document preferred assertion patterns (ShouldSatisfyAllConditions vs direct assertions).  
**Effort:** Small  
**Test Strategy:** Documentation only.  
- [ ] TD0059

### TD0060 - Standardize test naming conventions check
**File(s):** All test files  
**Severity:** Low  
**Impact:** Low  
**Description:** Verify all test files follow `<MethodName>_<Scenario>_<ExpectedOutcome>` naming.  
**Effort:** Small  
**Test Strategy:** Naming audit only - no code changes needed.  
- [ ] TD0060

---

## How to Use This Document

### Working on Technical Debt Tasks

1. **Choose a task** based on severity/impact and current priorities
2. **Write tests first** that verify current behavior (unless it's a "simple change" exception)
3. **Run tests** to confirm they pass
4. **Implement the refactoring**
5. **Run tests again** to verify nothing broke
6. **Update the checkbox** from `[ ]` to `[x]` to mark complete
7. **Commit** with reference to the task ID in commit message

### Priority Guidelines

**High Severity + High Impact:** Do first - these are critical code quality issues  
**High Severity + Low Impact:** Do soon - important but less urgent  
**Low Severity + High Impact:** Do when convenient - good ROI  
**Low Severity + Low Impact:** Do last or skip - nice to have

### Asking Permission to Skip Tests

If you encounter a situation where:
- The code is too tightly coupled to test in isolation
- Writing tests would require significant refactoring first
- The change is purely organizational (moving code without changing logic)

**ASK the user for permission** before skipping the mandatory testing rule.

---

## Progress Summary

**Total Tasks:** 60  
**Completed:** 0  
**In Progress:** 0  
**Remaining:** 60

### By Category
- Backend Duplicated Code: 8 tasks
- Backend SRP Violations: 13 tasks
- Backend Consistency: 9 tasks
- DTO Consistency: 11 tasks
- Frontend Violations: 9 tasks
- Cross-Project Utilities: 6 tasks
- Testing Patterns: 4 tasks

### By Severity
- High: 17 tasks
- Medium: 32 tasks
- Low: 11 tasks
