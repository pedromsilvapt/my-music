using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;
using MyMusic.Common.Filters;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Queries <see cref="DeviceSyncSessionRecord"/> rows for a single sync session, applying the
/// optional DSL filter, backward-compatible <c>actions</c> comma filter, sorting, and offset-based
/// pagination. Extracted from <see cref="MyMusic.Server.Controllers.DevicesController"/> so the
/// controller stays thin (input/output + DTO mapping only). Reuses
/// <see cref="ISyncSessionLookupService"/> for the session identity/ownership check so not-found
/// and foreign-device cases resolve to <c>null</c> (which the controller maps to
/// <c>NotFound</c>).
/// </summary>
public interface ISyncSessionRecordsQueryService
{
    /// <summary>
    /// Queries the records for the session identified by <paramref name="sessionId"/> on the
    /// device <paramref name="deviceId"/> owned by <paramref name="ownerId"/>. Returns
    /// <c>null</c> when no such session exists for the owner.
    /// </summary>
    Task<SyncSessionRecordsQueryResult?> QueryAsync(
        long sessionId,
        long deviceId,
        long ownerId,
        string? actions,
        int? limit,
        int? offset,
        string? sort,
        bool? includeSongInfo,
        string? filter,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a sync session records query operation. The controller maps this to
/// <c>ListSyncRecordsResponse</c>.
/// </summary>
public record SyncSessionRecordsQueryResult
{
    public required List<DeviceSyncSessionRecord> Records { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
    public int TotalCount { get; init; }
}

/// <summary>
/// Default implementation of <see cref="ISyncSessionRecordsQueryService"/>.
/// </summary>
public class SyncSessionRecordsQueryService(
    MusicDbContext db,
    ISyncSessionLookupService sessionLookup) : ISyncSessionRecordsQueryService
{
    /// <inheritdoc />
    public async Task<SyncSessionRecordsQueryResult?> QueryAsync(
        long sessionId,
        long deviceId,
        long ownerId,
        string? actions,
        int? limit,
        int? offset,
        string? sort,
        bool? includeSongInfo,
        string? filter,
        CancellationToken cancellationToken)
    {
        var session = await sessionLookup.FindSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (session == null) return null;

        var query = db.DeviceSyncSessions
            .Where(s => s.Id == sessionId)
            .SelectMany(s => s.Records);

        // Conditionally include Song and Artists if requested
        if (includeSongInfo == true)
        {
            query = query
                .Include(r => r.Song)
                .ThenInclude(s => s.Artists)
                .ThenInclude(a => a.Artist);
        }

        // Apply filter DSL if provided
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<DeviceSyncSessionRecord>(filter, GetSessionRecordFieldMappings());
            query = query.Where(filterExpression);
        }

        // Keep existing actions filter for backward compatibility
        if (!string.IsNullOrEmpty(actions))
        {
            var actionList = actions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(a => Enum.Parse<SyncRecordAction>(a, true))
                .ToHashSet();
            query = query.Where(r => actionList.Contains(r.Action));
        }

        // Get total count for pagination metadata
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        IOrderedQueryable<DeviceSyncSessionRecord> orderedQuery;
        if (sort == "action_date")
        {
            orderedQuery = query
                .OrderBy(r => r.Action)
                .ThenBy(r => r.ProcessedAt)
                .ThenBy(r => r.Id);
        }
        else
        {
            orderedQuery = query.OrderBy(r => r.Id);
        }

        // Apply offset-based pagination
        if (offset.HasValue && offset.Value > 0)
        {
            orderedQuery = (IOrderedQueryable<DeviceSyncSessionRecord>)orderedQuery.Skip(offset.Value);
        }

        List<DeviceSyncSessionRecord> records;
        string? nextCursor = null;
        bool hasMore = false;

        if (limit.HasValue)
        {
            // Fetch one extra to determine if there are more records
            records = await orderedQuery
                .Take(limit.Value + 1)
                .ToListAsync(cancellationToken);

            // Check if we have more records
            if (records.Count > limit.Value)
            {
                hasMore = true;
                records.RemoveAt(records.Count - 1); // Remove the extra record
                var currentOffset = offset ?? 0;
                nextCursor = (currentOffset + limit.Value).ToString();
            }
        }
        else
        {
            // No limit specified - return all records (backward compatibility)
            records = await orderedQuery.ToListAsync(cancellationToken);
        }

        return new SyncSessionRecordsQueryResult
        {
            Records = records,
            NextCursor = nextCursor,
            HasMore = hasMore,
            TotalCount = totalCount,
        };
    }

    /// <summary>
    /// DSL field-name → entity-path mappings used by the dynamic filter for session records.
    /// Public so the controller (and tests) can reference the same set if needed.
    /// </summary>
    public static Dictionary<string, string> GetSessionRecordFieldMappings()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["song"] = "Song.SearchableText",
            ["song.title"] = "Song.Title",
            ["song.artist.name"] = "Song.Artists.Artist.Name",
            ["song.album.name"] = "Song.Album.Name",
        };
    }
}