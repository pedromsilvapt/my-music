namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Returns the distinct values for a single sync-session record filter field, scoped to the
/// current owner and (for file paths) the target session/device. Extracted from
/// <see cref="MyMusic.Server.Controllers.DevicesController"/> so the controller stays thin
/// (input/output + DTO mapping only). The filter metadata endpoint (pure static output) remains
/// in the controller.
/// </summary>
public interface ISyncSessionFilterValuesService
{
    /// <summary>
    /// Returns the distinct, ordered, optionally searched, and limited values for the given
    /// <paramref name="field"/> for the session identified by <paramref name="sessionId"/> on the
    /// device <paramref name="deviceId"/> owned by <paramref name="ownerId"/>. Unknown fields
    /// return an empty list (mirrors the prior controller behavior).
    /// </summary>
    Task<SyncSessionFilterValuesResult> GetAsync(
        long ownerId,
        long deviceId,
        long sessionId,
        string field,
        string? search,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a sync session filter-values operation. The controller maps this to
/// <c>FilterValuesResponse</c>.
/// </summary>
public record SyncSessionFilterValuesResult
{
    public required List<string> Values { get; init; }
}