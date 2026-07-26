using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Queries <see cref="DeviceSyncSessionRecord"/> rows for a single sync session, applying the
/// optional DSL filter, backward-compatible <c>actions</c> comma filter, sorting, and offset-based
/// pagination. Reuses <see cref="ISyncSessionLookupService"/> for the session identity/ownership
/// check so not-found and foreign-device cases resolve to <c>null</c> (which the controller maps
/// to <c>NotFound</c>).
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