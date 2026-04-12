## 1. Database & Data Model

- [x] 1.1 Add nullable `int? Bitrate` property to Song entity (MyMusic.Common/Entities/Song.cs)
- [x] 1.2 Create EF Core migration to add Bitrate column to Songs table
- [x] 1.3 Apply migration and verify database schema updated

## 2. Backend - Bitrate Extraction

- [x] 2.1 Add `int? Bitrate` property to SongMetadata class (MyMusic.Common/Metadata/SongMetadata.cs)
- [x] 2.2 Extract bitrate from TagLib.Properties.AudioBitrate in TagConverter.ToSong method
- [x] 2.3 Update MusicService to store bitrate during song import
- [x] 2.4 Add bitrate to metadata re-import/refresh logic
- [x] 2.5 Add logging for bitrate extraction failures (warning level, continue import)
- [x] 2.6 Write unit tests for bitrate extraction (TagConverterSpecs)
- [x] 2.7 Write integration tests for song import with bitrate (MusicServiceSpecs)

## 3. Backend - Soundalike API DTOs

- [x] 3.1 Create SoundalikeGroupDto with songs, matchScore, pairwiseScores, signature
- [x] 3.2 Create SoundalikeSongDto with all metadata fields including artwork preview URL
- [x] 3.3 Create ResolveSoundalikesRequest with list of group resolutions
- [x] 3.4 Create GroupResolution with primarySongId and secondarySongIds
- [x] 3.5 Create ResolveSoundalikesResponse with resolved count
- [x] 3.6 Create mapping methods from entities to DTOs

## 4. Backend - Soundalike API Endpoints

- [x] 4.1 Create GET /api/audits/soundalikes endpoint in AuditsController
- [x] 4.2 Implement query to fetch AuditNonConformities with SoundalikeGroupData
- [x] 4.3 Eager load song metadata using IncludeSongMetadata extension
- [x] 4.4 Map entities to DTOs with artwork preview URLs
- [x] 4.5 Order groups by matchScore descending
- [x] 4.6 Filter to only include groups owned by authenticated user
- [x] 4.7 Write integration tests for GET endpoint (AuditsControllerSpecs)

## 5. Backend - Metadata Merge Logic

- [x] 5.1 Create SoundalikeMergeService with MergeMetadata method
- [x] 5.2 Implement null-coalescing merge for year, lyrics, artwork, bitrate
- [x] 5.3 Implement merge of unique artists from all secondaries
- [x] 5.4 Implement merge of unique genres from all secondaries
- [x] 5.5 Write unit tests for all merge scenarios (SoundalikeMergeServiceSpecs)

## 6. Backend - Resolution & Delete Logic

- [x] 6.1 Create POST /api/audits/soundalikes/resolve endpoint in AuditsController
- [x] 6.2 Validate all songs are owned by authenticated user (403 if not)
- [x] 6.3 Validate each group has exactly one primary song ID (400 if not)
- [x] 6.4 Call SoundalikeMergeService.MergeMetadata for each group
- [x] 6.5 Delete PlaylistSong records for secondary songs
- [x] 6.6 Mark SongDevice records for removal: set SongId = null, Action = Remove (DO NOT delete the records)
- [x] 6.7 Delete secondary Song entities
- [x] 6.8 Delete AuditNonConformity record for the group
- [x] 6.9 Wrap all operations in database transaction
- [x] 6.10 Write integration tests for resolve endpoint (AuditsControllerSpecs)

## 7. Frontend - TypeScript Types & Guards

- [x] 7.1 Create SoundalikeGroupData type matching backend DTO
- [x] 7.2 Create SoundalikeSong type with all metadata fields
- [x] 7.3 Create type guard isSoundalikeGroupData for runtime validation
- [x] 7.4 Create discriminated union for AuditNonConformityData types
- [x] 7.5 Create ResolveSoundalikesRequest and GroupResolution types

## 8. Frontend - API Client

- [x] 8.1 Regenerate Orval client after adding backend endpoints
- [x] 8.2 Verify generated hooks: useGetSoundalikes, useResolveSoundalikes
- [x] 8.3 Add invalidation rules to orval.config.cjs for resolve mutation

## 9. Frontend - Soundalike Page Component

- [x] 9.1 Create route file at MyMusic.Client/src/routes/audits/soundalike.tsx
- [x] 9.2 Create SoundalikePage component with group list display
- [x] 9.3 Create SoundalikeGroup component for each group
- [x] 9.4 Create SoundalikeSongCard component for song display with all metadata
- [x] 9.5 Add artwork preview with hover tooltip
- [x] 9.6 Display match score percentage for each group
- [x] 9.7 Implement loading state and error handling

## 10. Frontend - Selection Logic

- [x] 10.1 Create useSoundalikeSelection hook with Zustand store
- [x] 10.2 Implement primary song selection (single per group)
- [x] 10.3 Implement secondary song selection (multiple per group)
- [x] 10.4 Add visual distinction for primary (highlighted) and secondary (strikethrough/faded)
- [x] 10.5 Implement click handlers for song cards

## 11. Frontend - Merge Preview

- [x] 11.1 Create MergePreview component
- [x] 11.2 Calculate which fields will be merged based on primary and secondaries
- [x] 11.3 Display fields that will change on primary (highlighted)
- [x] 11.4 Show preview when primary and secondaries are selected

## 12. Frontend - Remove Duplicates Button

- [x] 12.1 Create Remove Duplicates button component
- [x] 12.2 Calculate count from selected groups (show N in button text)
- [x] 12.3 Disable button when no groups have primary selection
- [x] 12.4 Create confirmation dialog with song count
- [x] 12.5 Show merged metadata summary in confirmation dialog
- [x] 12.6 Call resolve API mutation on confirm
- [x] 12.7 Handle success: remove resolved groups, show success toast
- [x] 12.8 Handle error: show error toast, keep groups in current state
- [x] 12.9 Add loading state during API call

## 13. Testing & Verification

- [x] 13.1 Run all backend tests: `dotnet test`
- [x] 13.2 Run frontend type check: `npm run lint` (MyMusic.Client)
- [x] 13.3 Run frontend lint: `npm run lint`
- [x] 13.4 Test bitrate extraction with real MP3 and FLAC files
- [x] 13.5 Test complete workflow: import → detect → display → select → resolve → delete
- [x] 13.6 Test error scenarios: unauthorized access, invalid requests, failed extraction
- [x] 13.7 Test concurrent access scenarios if applicable

## 14. Documentation

- [x] 14.1 Update AGENTS.md if any new patterns or conventions introduced
- [x] 14.2 Verify OpenAPI spec correctly documents new endpoints (regenerate if needed)
