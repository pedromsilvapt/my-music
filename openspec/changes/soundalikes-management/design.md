## Context

The MyMusic system already has acoustic fingerprinting infrastructure that detects duplicate songs via `SoundalikeAuditRule`. However, the detected duplicates (stored as `AuditNonConformity` records) cannot be managed through the UI - users have no way to review, compare, and resolve them. The non-conformity data structure (`SoundalikeGroupData`) contains only SongIds, requiring additional API calls to fetch full song details.

Current song metadata extraction uses TagLib and captures title, artists, album, year, duration, lyrics, and artwork, but bitrate is not currently stored. The existing artwork system already supports thumbnail previews through `ThumbnailProxyService`.

## Goals / Non-Goals

**Goals:**
- Enable users to visually compare duplicate songs side-by-side with all metadata displayed
- Allow users to select which song to keep (primary) and which to delete (secondaries)
- Automatically merge missing metadata from secondary songs to primary before deletion
- Store bitrate information for better comparison data
- Provide batch operation to resolve all selected groups at once

**Non-Goals:**
- Automatic duplicate resolution (user must make selections)
- Modifying the acoustic fingerprinting algorithm
- Moving files between repositories
- Bulk metadata editing beyond the merge workflow

## Decisions

### 1. Data Model: Add Bitrate to Song Entity

**Decision:** Add nullable `int? Bitrate` property to Song entity.

**Rationale:** 
- Bitrate is a standard audio property useful for quality comparison
- Nullable allows backward compatibility with existing songs
- TagLib already exposes this via `Properties.AudioBitrate`

**Alternatives considered:**
- Store in JSON metadata field: Makes querying/filtering difficult
- Create separate AudioProperties entity: Overkill for single field, adds join complexity

### 2. API Design: Two-Endpoint Approach

**Decision:** Create two endpoints:
- `GET /api/audits/soundalikes` - Returns groups with fully hydrated song details
- `POST /api/audits/soundalikes/resolve` - Accepts resolution requests and processes merge+delete

**Rationale:**
- Separation of concerns: read vs. write operations
- The GET endpoint can use existing `IncludeSongMetadata` extension for eager loading
- The POST endpoint handles the transactional merge+delete workflow
- Allows different caching strategies for read vs. write

**Alternatives considered:**
- Single endpoint with query params: More complex, harder to test
- GraphQL-style flexible query: Overkill, adds complexity, existing pattern uses REST

### 3. Merge Strategy: Null-Coalescing with Primary Preference

**Decision:** When merging metadata from secondaries to primary, use null-coalescing where the primary's existing values take precedence, and secondary values only fill in missing fields.

**Merge rules:**
- Title: Keep primary's title (never merge)
- Artists: Merge unique artists from all secondaries
- Album: Keep primary's album
- Year: Use primary's year if set, else first secondary with year
- Genres: Merge unique genres from all secondaries
- Lyrics: Use primary's lyrics if set, else first secondary with lyrics
- Artwork: Keep primary's artwork if exists, else first secondary with artwork
- Bitrate: Keep primary's bitrate if set, else first secondary with bitrate

**Rationale:**
- Simple, predictable behavior users can understand
- Primary selection represents "best" version - its existing metadata should be respected
- Prevents accidental overwrites of good metadata with bad

**Alternatives considered:**
- Conflict resolution UI: Too complex, slows down workflow
- Average/combine numeric values: Doesn't make sense for metadata
- Use most complete song: Unpredictable, hard to explain

### 4. Frontend Architecture: TypeScript Type Guards for Data Column

**Decision:** Create discriminated union types with type guards for the `Data` column from `AuditNonConformity`.

**Structure:**
```typescript
type NonConformityData = SoundalikeGroupData | MissingCoverData | ...;

function isSoundalikeGroupData(data: unknown): data is SoundalikeGroupData {
  return typeof data === 'object' && 
         data !== null && 
         'songIds' in data && 
         'matchScore' in data;
}
```

**Rationale:**
- Existing pattern in codebase: DTO records map from entities
- Type safety catches errors at compile time
- Guards provide runtime validation for JSON data from API
- Matches existing TypeScript conventions in MyMusic.Client

**Alternatives considered:**
- `any` type: No type safety, defeats purpose of TypeScript
- Zod validation: Would need additional dependency, guards sufficient for this use case

### 5. Delete Operation: Transactional with Playlist Cleanup

**Decision:** Deleting secondary songs must:
1. Remove from all playlists (`PlaylistSong` records)
2. Mark for removal from devices: For each `SongDevice` record, set `SongId` to null and `Action = Remove` (DO NOT delete the SongDevice record)
3. Delete the song record
4. Delete the audit non-conformity record for the group
5. All within a single database transaction

**Rationale:**
- Ensures referential integrity
- Prevents orphaned records
- Single transaction allows rollback on any failure
- **Critical:** Preserving `SongDevice` records with `SongId = null` and `Action = Remove` allows the sync system to properly track and remove files from devices during the next sync operation
- Existing EF Core cascade behavior handles most relationships

**Alternatives considered:**
- Delete `SongDevice` records: Would break sync tracking - devices would not know to remove the deleted files
- Soft delete on Song: Adds complexity, still needs SongDevice handling
- Let cascade deletes handle it: Would delete SongDevice records, breaking sync functionality

## Risks / Trade-offs

**Risk: Large groups could cause performance issues** → Mitigation: Paginate groups or limit display to N songs per group initially with "show more"

**Risk: User accidentally deletes wrong song** → Mitigation: Require explicit primary selection, show confirmation dialog with count, make primary visually distinct

**Risk: Metadata merge produces unexpected results** → Mitigation: Show preview of merged metadata before confirming deletion, highlight which fields will change

**Risk: Bitrate extraction fails on some files** → Mitigation: Make field nullable, log warnings, continue import without failing

**Risk: Concurrent edits during resolution** → Mitigation: Use optimistic concurrency with RowVersion on Song entity (if not already present), or use EF Core's built-in transaction isolation
