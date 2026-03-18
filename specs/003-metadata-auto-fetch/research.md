# Phase 0: Research Findings

**Date**: 2026-03-17  
**Feature**: Auto-Fetch Metadata for Audit Issues

## Research Summary

All unknowns have been investigated and resolved. The existing infrastructure supports this feature well.

---

## Decision: Background Queue Implementation

**Rationale**: The codebase has a proven `BackgroundTaskScheduler<TTask>` pattern demonstrated by `PurchasesQueue`. This should be mirrored exactly.

**Pattern Structure**:
1. **Entity** - Store task state (Queued/Processing/Completed/Failed)
2. **Queue Service** - Inherits `BackgroundTaskScheduler<TTask>`, manages lifecycle
3. **Scheduler** - Nested class, pulls tasks from DB, manages status transitions
4. **Executor** - Nested class, contains actual business logic
5. **Controller** - API endpoint to create tasks and trigger scheduling

**Key Files**:
- `/workspaces/my-music/MyMusic.Common/Utilities/BackgroundTaskScheduler.cs` - Abstract base
- `/workspaces/my-music/MyMusic.Common/Services/PurchasesQueue.cs` - Reference implementation
- `/workspaces/my-music/MyMusic.Server/Controllers/PurchasesController.cs` - API pattern

**Implementation Notes**:
- Status enum: Queued=0, Processing=1, Completed=2, Failed=3
- Must call `TryScheduleTasksAsync()` after saving to DB
- Use `IServiceScopeFactory` for dependency resolution in background tasks

---

## Decision: Metadata Storage Format

**Rationale**: User explicitly requested JsonElement for flexibility in storing metadata patches.

**Storage Strategy**:
- Entity: `AutoFetchedMetadata` with `JsonElement MetadataPatch` property
- EF Core configuration: Use `HasJsonPropertyName` or value converter for JSON column
- Schema: Store the full `SongMetadataDiff` object as JSON
- Additional fields: SongId, Status (Pending/Applied/Failed), SourceId, FetchedAt, ErrorMessage

**Example Structure**:
```csharp
public class AutoFetchedMetadata
{
    public long Id { get; set; }
    public long SongId { get; set; }
    public JsonElement MetadataPatch { get; set; }  // SongMetadataDiff as JSON
    public AutoFetchStatus Status { get; set; }
    public long? SourceId { get; set; }  // Which source provided the data
    public DateTime FetchedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
```

---

## Decision: Sources API Integration

**Rationale**: Must reuse existing sources API for consistency with edit modal behavior.

**Integration Point**:
- Service: `ISourcesService` at `/workspaces/my-music/MyMusic.Common/Services/ISourcesService.cs`
- Method: `GetSourceClientAsync(long sourceId)` returns `ISource` (Refit client)
- Search: `ISource.SearchSongsAsync(query)` returns `List<SourceSong>`
- Details: `ISource.GetSongAsync(id)` returns full `SourceSong`

**Implementation Pattern**:
```csharp
// In MetadataFetchExecutor
var sources = await _db.Sources.ToListAsync(ct);
foreach (var source in sources)
{
    var client = await _sourcesService.GetSourceClientAsync(source.Id, ct);
    var results = await client.SearchSongsAsync($"{song.Title} {artists}", ct);
    // Match and fetch details...
}
```

**Alternatives Rejected**:
- Direct MusicBrainz/Discogs integration - Would bypass configured sources
- New API endpoints - Unnecessary, existing sources system is sufficient

---

## Decision: Audit Rule to Field Mapping

**Rationale**: User selected option B - rule-based mapping with composite selections.

**Audit Rules (IDs from research)**:
| ID | Rule Name | Pre-Selected Fields |
|----|-----------|---------------------|
| 1 | Missing Cover | `cover` |
| 2 | Missing Year | `year` |
| 3 | Missing Genres | `genres` |
| 4 | Missing Lyrics | `lyrics` |
| 5 | Medium Cover | `cover` |
| 6 | Small Cover | `cover` |
| 7 | Non-JPEG Cover | `cover` |
| 8 | Non-Square Cover | `cover` |

**Mapping Implementation**:
- Create `AuditRuleFieldMapper` service
- Method: `GetFieldsForRule(long ruleId) -> List<string>`
- Special case: If multiple issues exist for a song, pre-select fields from all applicable rules
- UI will merge these with the checkboxes derived from actual metadata

---

## Decision: Edit Modal Integration

**Rationale**: Modal already supports external metadata via props, just needs to be populated.

**Current State**:
- Modal accepts `metadata?: Map<number, SongMetadataDiff>` prop
- `checkboxesFromMetadata()` pre-checks fields that have metadata
- Need to add pre-selection based on audit rule type

**Implementation Changes**:
1. API endpoint: When opening edit from audit page, check for `AutoFetchedMetadata`
2. If found, deserialize `MetadataPatch` (JsonElement) to `SongMetadataDiff`
3. Pass to modal via `metadata` prop
4. Merge rule-based pre-selections with metadata-based checkboxes

**File Locations**:
- `/workspaces/my-music/MyMusic.Client/src/components/songs/song-editor-modal.tsx` - Main modal
- `/workspaces/my-music/MyMusic.Client/src/components/songs/song-edit-types.ts` - Checkbox logic

---

## Key Files Reference

### Backend
- `MyMusic.Common/Utilities/BackgroundTaskScheduler.cs` - Queue base class
- `MyMusic.Common/Services/PurchasesQueue.cs` - Reference queue implementation
- `MyMusic.Common/Services/ISourcesService.cs` - Sources API interface
- `MyMusic.Common/Entities/AuditNonConformity.cs` - Audit entity
- `MyMusic.Server/Controllers/SongsController.cs` - Has FetchMetadata endpoint (lines 944-1018)
- `MyMusic.Server/DTO/Songs/FetchMetadataResponse.cs` - Metadata diff structure

### Frontend
- `MyMusic.Client/src/components/songs/song-editor-modal.tsx` - Edit modal
- `MyMusic.Client/src/components/songs/song-edit-types.ts` - Checkbox types
- `MyMusic.Client/src/client/songs.ts` - API client (auto-generated)

---

## Clarifications Resolved

All [NEEDS CLARIFICATION] markers from the specification have been resolved:

1. ✅ **Background Queue**: Will mirror PurchasesQueue pattern exactly
2. ✅ **Sources API**: Will reuse existing ISourcesService and ISource interfaces
3. ✅ **Audit Rule Mapping**: Rule-based approach with specific field mappings
4. ✅ **Metadata Storage**: JsonElement as requested
5. ✅ **Edit Modal Integration**: Leverage existing metadata prop support

---

## Next Steps

1. Create `AutoFetchedMetadata` entity with JsonElement field
2. Create `MetadataFetchQueue` mirroring PurchasesQueue pattern
3. Create API endpoint for triggering batch fetch
4. Update edit modal to load auto-fetched metadata
5. Implement rule-to-field mapping for pre-selection
