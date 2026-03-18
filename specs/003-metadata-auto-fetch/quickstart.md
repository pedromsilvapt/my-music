# Quick Start Guide: Auto-Fetch Metadata

**Feature**: Auto-Fetch Metadata for Audit Issues  
**Setup Time**: ~30 minutes  
**Prerequisites**: .NET 9.0 SDK, PostgreSQL, Node.js 18+

---

## Initial Setup

### 1. Database Migration

Create and apply the database migration for new entities:

```bash
cd /workspaces/my-music
dotnet ef migrations add AddAutoFetchedMetadata \
  --project MyMusic.Common \
  --startup-project MyMusic.Server
```

This creates:
- `AutoFetchedMetadata` table
- `MetadataFetchTask` table
- Required indexes

### 2. Verify Migration

Check the generated migration file in:
`MyMusic.Common/Migrations/[timestamp]_AddAutoFetchedMetadata.cs`

Ensure it includes:
- Both entity tables
- FK relationships to Song and Source
- JSON column for MetadataPatch
- All indexes defined in data-model.md

### 3. Apply Migration

```bash
dotnet run --project MyMusic.Server
```

The server automatically applies pending migrations on startup.

---

## Development Workflow

### Running the Feature

**1. Start the Server**

```bash
cd /workspaces/my-music/MyMusic.Server
dotnet run
```

Server runs on `http://localhost:5000` with:
- Background task scheduler active
- API endpoints available
- Database migrations applied

**2. Start the Web Client**

```bash
cd /workspaces/my-music/MyMusic.Client
npm run dev
```

Client runs on `http://localhost:5173`

**3. Access the Audits Page**

Navigate to: `http://localhost:5173/audits`

You should see the new "Auto-fetch Metadata" button on eligible audit issues.

---

## Testing the Feature

### Manual Testing Steps

**Test 1: Trigger Batch Fetch**

1. Go to Audits page (`/audits`)
2. Ensure there are songs with non-waived audit issues
3. Click "Auto-fetch Metadata" button
4. Verify:
   - Confirmation message appears
   - Tasks are created (check `/api/metadata-fetch/queue-status`)
   - Background tasks start processing

**Test 2: Background Task Processing**

1. Wait for tasks to process (or check logs)
2. Query database:
   ```sql
   SELECT * FROM auto_fetched_metadata WHERE status = 0; -- Pending
   ```
3. Verify metadata patches were stored as JSON

**Test 3: Edit Song with Auto-Fetched Metadata**

1. Go to Audits page
2. Click "Edit" on a song with pending auto-fetched metadata
3. Verify:
   - Modal opens with metadata suggestions
   - Pre-selected checkboxes match audit rule type
   - Old/new values displayed side-by-side
4. Modify checkboxes and save
5. Verify metadata status changes to "Applied"

**Test 4: 30-Day Deduplication**

1. Try to auto-fetch metadata for same song again
2. Verify song is excluded (no duplicate tasks created)

---

## Running Tests

### Backend Tests

```bash
# All tests
dotnet test

# Specific feature tests
dotnet test --filter "FullyQualifiedName~MetadataFetch"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Frontend Tests

```bash
cd MyMusic.Client
npm test

# Or with coverage
npm run test:coverage
```

---

## Regenerating API Client

After modifying API endpoints:

```bash
# 1. Ensure server is running to update OpenAPI
cd MyMusic.Server
dotnet run &

# 2. Wait 5 seconds for startup

# 3. Regenerate client
devbox run orval

# 4. Kill server background process
pkill -f "dotnet run"
```

Client files are auto-generated in:
- `MyMusic.Client/src/client/metadata-fetch.ts`

**Important**: Never manually edit files in `src/client/` or `src/model/`

---

## Debugging

### Server Logs

Enable detailed logging:

```bash
export LOG_LEVEL=Debug
dotnet run --project MyMusic.Server
```

Check for:
- Background task scheduling messages
- Metadata fetch attempts
- Error messages from sources API

### Database Inspection

Connect to PostgreSQL:

```bash
psql -h localhost -U mymusic -d mymusic
```

Useful queries:

```sql
-- Check pending metadata fetches
SELECT song_id, fetched_at, status 
FROM auto_fetched_metadata 
WHERE status = 0;

-- Check task queue status
SELECT status, COUNT(*) 
FROM metadata_fetch_tasks 
GROUP BY status;

-- Find songs eligible for fetch
SELECT s.id, s.title, COUNT(nc.id) as issue_count
FROM songs s
JOIN audit_non_conformities nc ON nc.song_id = s.id
WHERE nc.has_waiver = false
  AND NOT EXISTS (
    SELECT 1 FROM auto_fetched_metadata afm
    WHERE afm.song_id = s.id
    AND afm.status IN (0, 1)  -- Pending or Applied
    AND afm.fetched_at > NOW() - INTERVAL '30 days'
  )
GROUP BY s.id, s.title;
```

### Client Debugging

Browser DevTools:
- Network tab: Watch for `/api/metadata-fetch/*` requests
- Console: Check for React errors or state issues
- React DevTools: Inspect modal state and checkbox selections

---

## Common Issues

### Issue: Background tasks not processing

**Symptoms**: Tasks stay in "Queued" status

**Solutions**:
1. Check server logs for scheduler errors
2. Verify `MetadataFetchQueue` is registered in DI:
   ```bash
   grep -r "MetadataFetchQueue" MyMusic.Common/HostBuilderExtensions.cs
   ```
3. Ensure `TryScheduleTasksAsync()` is called after task creation

### Issue: Metadata not showing in edit modal

**Symptoms**: Modal opens without auto-fetched suggestions

**Solutions**:
1. Verify `AutoFetchedMetadata` record exists with Status=0 (Pending)
2. Check API response from `GET /api/metadata-fetch/song/{id}`
3. Ensure modal is receiving `metadata` prop correctly

### Issue: Checkboxes not pre-selected

**Symptoms**: All checkboxes unchecked by default

**Solutions**:
1. Check audit rule ID to field mapping is correct
2. Verify `checkboxesFromMetadata()` is being called
3. Ensure `SongMetadataDiff` is properly deserialized from JSON

### Issue: Orval generation fails

**Symptoms**: Client code not updated after API changes

**Solutions**:
1. Ensure server is running and accessible
2. Check OpenAPI spec at `http://localhost:5000/openapi/v1.json`
3. Verify `orval.config.cjs` includes new endpoints

---

## Performance Considerations

### Database

- Index on `(SongId, Status)` for quick pending lookups
- Index on `(SongId, FetchedAt)` for 30-day window queries
- JsonElement storage is efficient for PostgreSQL JSONB columns

### Background Tasks

- Default parallelization: 3 concurrent tasks
- Each task queries all configured sources
- Progress tracking: 0→50% for source queries, 50→100% for storage

### API

- Batch endpoint is fast (< 5 seconds) - only creates task records
- Individual song endpoint caches metadata in state
- Queue status endpoint is lightweight (counts only)

---

## Next Steps

1. ✅ Setup complete - database migrated
2. 🔄 Implement backend entities and services
3. 🔄 Create API endpoints
4. 🔄 Implement background task queue
5. 🔄 Update frontend edit modal
6. 🔄 Add audit page button
7. 🔄 Write tests
8. 🔄 Manual testing

Run `/speckit.tasks` to generate the task breakdown.
