# Data Model: Auto-Fetch Metadata

**Feature**: Auto-Fetch Metadata for Audit Issues  
**Date**: 2026-03-17

---

## Entity: AutoFetchedMetadata

Stores metadata patches fetched from external sources for songs with audit issues.

### Properties

| Property | Type | Constraints | Description |
|----------|------|-------------|-------------|
| Id | long | PK, auto-increment | Unique identifier |
| SongId | long | FK → Song.Id, CASCADE delete | Link to song |
| MetadataPatch | JsonElement | Required, JSON column | The metadata diff patch as JSON |
| Status | AutoFetchStatus | Required | Current state of the fetch |
| SourceId | long? | FK → Source.Id, nullable | Which external source provided the data |
| FetchedAt | DateTime | Required | When the metadata was fetched |
| ErrorMessage | string? | Max 500 chars | Error details if fetch failed |

### Status Enum

```csharp
public enum AutoFetchStatus
{
    Pending = 0,    // Fetched but not yet viewed/applied
    Applied = 1,    // User has applied the metadata
    Failed = 2,     // Fetch operation failed
    Expired = 3     // Older than 30 days, should be re-fetched
}
```

### JSON Schema for MetadataPatch

The `MetadataPatch` field stores a serialized `SongMetadataDiff`:

```json
{
  "title": { "old": "Original Title", "new": "Fetched Title" },
  "year": { "old": 2020, "new": 2021 },
  "lyrics": { "old": null, "new": "Fetched lyrics..." },
  "explicit": { "old": false, "new": true },
  "cover": { "old": "old-cover-id", "new": "new-cover-url" },
  "album": { 
    "old": { "id": 1, "title": "Old Album" },
    "new": { "id": 0, "title": "New Album Title" }
  },
  "albumArtist": { "old": "Old Artist", "new": "New Artist" },
  "artists": {
    "old": [{ "id": 1, "name": "Artist 1" }],
    "new": [{ "id": 0, "name": "Artist 1" }, { "id": 0, "name": "Artist 2" }]
  },
  "genres": {
    "old": ["Rock"],
    "new": ["Rock", "Alternative"]
  }
}
```

---

## Entity: MetadataFetchTask

Background task queue entity (mirrors PurchasedSong pattern).

### Properties

| Property | Type | Constraints | Description |
|----------|------|-------------|-------------|
| Id | long | PK, auto-increment | Unique identifier |
| SongId | long | FK → Song.Id, CASCADE delete | Song to fetch metadata for |
| Status | MetadataFetchStatus | Required | Task state |
| Progress | int | 0-100 | Completion percentage |
| ErrorMessage | string? | Max 500 chars | Error if failed |
| CreatedAt | DateTime | Required | When task was queued |
| StartedAt | DateTime? | nullable | When processing began |
| CompletedAt | DateTime? | nullable | When processing finished |

### Status Enum

```csharp
public enum MetadataFetchStatus
{
    Queued = 0,      // Waiting to be processed
    Processing = 1,    // Currently fetching metadata
    Completed = 2,   // Successfully fetched and stored
    Failed = 3       // Failed after retries
}
```

---

## Entity: AuditNonConformity (Existing)

**Reference Only** - Already exists in system.

### Key Fields for This Feature

| Property | Type | Notes |
|----------|------|-------|
| SongId | long | FK to Song |
| AuditRuleId | long | 1-8 (see mapping below) |
| HasWaiver | bool | false = issue not waived |

### Audit Rule IDs

| ID | Rule | Pre-Selected Field(s) |
|----|------|----------------------|
| 1 | MissingCover | cover |
| 2 | MissingYear | year |
| 3 | MissingGenres | genres |
| 4 | MissingLyrics | lyrics |
| 5 | MediumCover | cover |
| 6 | SmallCover | cover |
| 7 | NonJpegCover | cover |
| 8 | NonSquareCover | cover |

---

## Entity: Song (Existing)

**Reference Only** - Already exists.

Key relationships:
- One-to-many with `AutoFetchedMetadata` (via SongId FK)
- One-to-many with `AuditNonConformity` (via SongId FK)
- One-to-many with `MetadataFetchTask` (via SongId FK)

---

## Entity: Source (Existing)

**Reference Only** - Already exists.

Used by:
- `AutoFetchedMetadata.SourceId` - which source provided the metadata
- `MetadataFetchTask` executor - which sources to query

---

## Relationships

```
Song (1)
├── AutoFetchedMetadata (0..n)
├── AuditNonConformity (0..n)
└── MetadataFetchTask (0..n)

AutoFetchedMetadata (n)
└── Source (0..1) [nullable]

MetadataFetchTask (n)
└── Song (1)
```

---

## Database Indexes

### AutoFetchedMetadata

```csharp
// For querying by song + status (finding pending metadata)
entity.HasIndex(e => new { e.SongId, e.Status });

// For 30-day window queries (fetch deduplication)
entity.HasIndex(e => new { e.SongId, e.FetchedAt });

// For status-based cleanup (expired records)
entity.HasIndex(e => new { e.Status, e.FetchedAt });
```

### MetadataFetchTask

```csharp
// For pulling queued tasks (scheduler)
entity.HasIndex(e => new { e.Status, e.CreatedAt });

// For querying by song (checking if already queued)
entity.HasIndex(e => new { e.SongId, e.Status });
```

---

## Validation Rules

### AutoFetchedMetadata

1. **Status transitions**:
   - Pending → Applied (user applied the metadata)
   - Pending → Expired (after 30 days)
   - Any → Failed (on error)

2. **Uniqueness**: A song can have multiple AutoFetchedMetadata records (history), but only one with Status = Pending at a time.

3. **JsonElement validation**: MetadataPatch must deserialize to a valid SongMetadataDiff structure.

### MetadataFetchTask

1. **Status transitions** (enforced by scheduler):
   - Queued → Processing → Completed
   - Queued → Processing → Failed
   - Failed → Queued (on retry)

2. **Deduplication**: Only one task per song with Status = Queued or Processing at a time.

3. **Progress tracking**: 0 when Queued, 0-99 when Processing, 100 when Completed/Failed.

---

## State Transitions

### AutoFetchedMetadata Lifecycle

```
[Background Task Creates]
        ↓
   ┌─────────┐
   │ PENDING │ ← User can view in edit modal
   └────┬────┘
        │
   ┌────┴────┬────────────────┐
   ↓         ↓                ↓
[User    [30 days      [Manual
applies]   passed]       cleanup]
   ↓         ↓                ↓
APPLIED   EXPIRED        (deleted)
```

### MetadataFetchTask Lifecycle

```
[API Endpoint Creates]
        ↓
   ┌────────┐
   │ QUEUED │ ← Waiting for scheduler
   └────┬───┘
        │
        ↓ (scheduler picks up)
   ┌───────────┐
   │ PROCESSING │ ← Fetching metadata
   └─────┬─────┘
         │
    ┌────┴────┐
    ↓         ↓
[Success] [Failure]
    ↓         ↓
COMPLETED  FAILED
               ↓
          [Retry logic]
               ↓
           QUEUED
```

---

## Query Patterns

### Find songs eligible for auto-fetch

```csharp
// Songs with non-waived issues AND no recent auto-fetched metadata
var eligibleSongs = await db.Songs
    .Where(s => db.AuditNonConformities
        .Any(nc => nc.SongId == s.Id && !nc.HasWaiver))
    .Where(s => !db.AutoFetchedMetadata
        .Any(afm => afm.SongId == s.Id 
            && afm.Status != AutoFetchStatus.Failed
            && afm.FetchedAt > DateTime.UtcNow.AddDays(-30)))
    .ToListAsync();
```

### Check for existing pending metadata

```csharp
var pendingMetadata = await db.AutoFetchedMetadata
    .Where(afm => afm.SongId == songId 
        && afm.Status == AutoFetchStatus.Pending)
    .OrderByDescending(afm => afm.FetchedAt)
    .FirstOrDefaultAsync();
```

### Get audit rules for a song

```csharp
var ruleIds = await db.AuditNonConformities
    .Where(nc => nc.SongId == songId && !nc.HasWaiver)
    .Select(nc => nc.AuditRuleId)
    .Distinct()
    .ToListAsync();
```
