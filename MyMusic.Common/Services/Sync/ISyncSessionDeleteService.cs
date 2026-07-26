namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Failure reasons returned by <see cref="ISyncSessionDeleteService.DeleteAsync"/>. The controller
/// maps each value to the appropriate HTTP response (NotFound / conflict) without leaking exceptions.
/// </summary>
public enum SyncSessionDeleteFailure
{
    NotFound,
    InProgress,
}

/// <summary>
/// Result of <see cref="ISyncSessionDeleteService.DeleteAsync"/>.
/// </summary>
public sealed class SyncSessionDeleteResult
{
    public bool Success { get; init; }

    public SyncSessionDeleteFailure? Failure { get; init; }

    public static SyncSessionDeleteResult Succeeded { get; } = new() { Success = true };

    public static SyncSessionDeleteResult Failed(SyncSessionDeleteFailure failure) => new() { Success = false, Failure = failure };
}

/// <summary>
/// Deletes a single <see cref="DeviceSyncSession"/> owned by the current user, along with its
/// <see cref="DeviceSyncSessionRecord"/> rows and staging directory. Reuses
/// <see cref="ISyncSessionLookupService"/> for the identity check and
/// <see cref="StagingDirectoryCleanupService"/> for staging cleanup.
/// </summary>
public interface ISyncSessionDeleteService
{
    /// <summary>
    /// Deletes the session <paramref name="sessionId"/> scoped to <paramref name="deviceId"/>
    /// owned by <paramref name="ownerId"/>. Recent in-progress sessions cannot be deleted and
    /// resolve to an <see cref="SyncSessionDeleteFailure.InProgress"/> failure.
    /// </summary>
    Task<SyncSessionDeleteResult> DeleteAsync(
        long sessionId,
        long deviceId,
        long ownerId,
        CancellationToken cancellationToken);
}