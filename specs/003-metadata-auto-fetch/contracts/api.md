# API Contract: Metadata Auto-Fetch

**Base Path**: `/api/metadata-fetch`  
**Feature**: Auto-Fetch Metadata for Audit Issues  
**Version**: 1.0.0

---

## Endpoints

### 1. Trigger Batch Metadata Fetch

Initiates background tasks to fetch metadata for all eligible songs.

**Endpoint**: `POST /api/metadata-fetch/batch`

**Request**: Empty body (uses current user's songs)

**Response**: `BatchMetadataFetchResponse`

```json
{
  "tasksCreated": 42,
  "message": "42 songs queued for metadata fetching"
}
```

**Status Codes**:
- `200 OK` - Tasks queued successfully
- `401 Unauthorized` - User not authenticated
- `429 Too Many Requests` - Rate limit exceeded

**Behavior**:
- Finds all songs with non-waived audit issues
- Excludes songs with auto-fetched metadata in last 30 days
- Excludes songs already queued for processing
- Creates MetadataFetchTask for each eligible song
- Returns count of tasks created

---

### 2. Get Auto-Fetched Metadata for Song

Retrieves pending auto-fetched metadata for a specific song.

**Endpoint**: `GET /api/metadata-fetch/song/{songId}`

**Path Parameters**:
- `songId` (long, required) - ID of the song

**Response**: `AutoFetchedMetadataResponse`

```json
{
  "hasMetadata": true,
  "metadata": {
    "title": { "old": "Original", "new": "Fetched Title" },
    "year": { "old": 2020, "new": 2021 },
    "album": { "old": { "id": 1, "title": "Old" }, "new": { "id": 0, "title": "New Album" } }
  },
  "fetchedAt": "2026-03-17T10:30:00Z",
  "sourceName": "MusicBrainz"
}
```

**Status Codes**:
- `200 OK` - Success (returns metadata or empty if none)
- `404 Not Found` - Song not found
- `401 Unauthorized` - User not authenticated

**Behavior**:
- Returns most recent Pending auto-fetched metadata
- Includes pre-selected fields based on audit rule types
- Returns empty response if no pending metadata exists

---

### 3. Apply Auto-Fetched Metadata

Marks auto-fetched metadata as applied after user saves changes.

**Endpoint**: `POST /api/metadata-fetch/song/{songId}/apply`

**Path Parameters**:
- `songId` (long, required) - ID of the song

**Response**: `ApplyMetadataResponse`

```json
{
  "success": true,
  "message": "Metadata marked as applied"
}
```

**Status Codes**:
- `200 OK` - Successfully marked as applied
- `404 Not Found` - Song or metadata not found
- `401 Unauthorized` - User not authenticated

**Behavior**:
- Updates AutoFetchedMetadata status from Pending to Applied
- Called automatically when user saves song with auto-fetched data

---

### 4. Get Metadata Fetch Queue Status

Returns current status of metadata fetch tasks for the user.

**Endpoint**: `GET /api/metadata-fetch/queue-status`

**Response**: `MetadataQueueStatusResponse`

```json
{
  "queued": 10,
  "processing": 2,
  "completed": 50,
  "failed": 3,
  "total": 65,
  "estimatedCompletion": "2026-03-17T11:00:00Z"
}
```

**Status Codes**:
- `200 OK` - Success
- `401 Unauthorized` - User not authenticated

---

### 5. Requeue Failed Tasks

Retries failed metadata fetch tasks.

**Endpoint**: `POST /api/metadata-fetch/requeue`

**Request**: `RequeueFailedRequest`

```json
{
  "taskIds": [101, 102, 103]
}
```

**Response**: `RequeueFailedResponse`

```json
{
  "requeuedCount": 3,
  "failedCount": 0
}
```

**Status Codes**:
- `200 OK` - Tasks requeued
- `400 Bad Request` - Invalid task IDs
- `401 Unauthorized` - User not authenticated

---

## DTO Specifications

### BatchMetadataFetchResponse

| Field | Type | Description |
|-------|------|-------------|
| tasksCreated | int | Number of tasks queued |
| message | string | Human-readable status message |

### AutoFetchedMetadataResponse

| Field | Type | Description |
|-------|------|-------------|
| hasMetadata | bool | Whether metadata exists |
| metadata | SongMetadataDiff | The metadata patch (if hasMetadata=true) |
| fetchedAt | DateTime? | When metadata was fetched |
| sourceName | string? | Name of the source that provided data |
| preSelectedFields | List<string> | Fields to pre-check based on audit rules |

### SongMetadataDiff

Same structure as existing `FetchMetadataResponse` from SongsController:

| Field | Type | Description |
|-------|------|-------------|
| title | SongMetadataField<string> | Title diff |
| year | SongMetadataField<int> | Year diff |
| lyrics | SongMetadataField<string> | Lyrics diff |
| rating | SongMetadataField<decimal> | Rating diff |
| explicit | SongMetadataField<bool> | Explicit flag diff |
| cover | SongMetadataField<string> | Cover ID/URL diff |
| album | SongMetadataField<SongMetadataAlbum> | Album diff |
| albumArtist | SongMetadataField<string> | Album artist diff |
| artists | SongMetadataField<List<SongMetadataArtist>> | Artists diff |
| genres | SongMetadataField<List<string>> | Genres diff |

### ApplyMetadataResponse

| Field | Type | Description |
|-------|------|-------------|
| success | bool | Whether operation succeeded |
| message | string | Status message |

### MetadataQueueStatusResponse

| Field | Type | Description |
|-------|------|-------------|
| queued | int | Tasks waiting to process |
| processing | int | Tasks currently processing |
| completed | int | Successfully completed tasks |
| failed | int | Failed tasks |
| total | int | Total tasks (sum of all) |
| estimatedCompletion | DateTime? | ETA for completion |

### RequeueFailedRequest

| Field | Type | Description |
|-------|------|-------------|
| taskIds | List<long> | IDs of failed tasks to retry |

### RequeueFailedResponse

| Field | Type | Description |
|-------|------|-------------|
| requeuedCount | int | Number successfully requeued |
| failedCount | int | Number that failed to requeue |

---

## Error Responses

All endpoints return standard error response on failure:

```json
{
  "error": {
    "code": "INVALID_REQUEST",
    "message": "Detailed error message",
    "details": {}
  }
}
```

**Error Codes**:
- `UNAUTHORIZED` - Authentication required
- `NOT_FOUND` - Resource not found
- `INVALID_REQUEST` - Bad request parameters
- `RATE_LIMITED` - Too many requests
- `INTERNAL_ERROR` - Server error

---

## Client Generation Notes

**Orval Configuration**:
- Add endpoints to OpenAPI spec
- Regenerate client with `devbox run orval`
- Client files will be in `MyMusic.Client/src/client/metadata-fetch.ts`

**Mutation Invalidation**:
Add to `orval.config.cjs`:
```javascript
mutationInvalidates: [
  ['batchMetadataFetch', ['metadataFetch', 'queueStatus']],
  ['applyMetadata', ['metadataFetch', 'song']],
  ['requeueFailedTasks', ['metadataFetch', 'queueStatus']]
]
```
