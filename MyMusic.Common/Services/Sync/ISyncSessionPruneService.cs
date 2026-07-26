namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Failure reason returned by <see cref="ISyncSessionPruneService.PruneAsync"/>. The controller
/// maps it to the appropriate HTTP response without leaking exceptions.
/// </summary>
public enum SyncSessionPruneFailure
{
    NotFound,
}

/// <summary>
/// Result of <see cref="ISyncSessionPruneService.PruneAsync"/>.
/// </summary>
public sealed class SyncSessionPruneResult
{
    public bool Success { get; init; }

    public int DeletedCount { get; init; }

    public SyncSessionPruneFailure? Failure { get; init; }

    public static SyncSessionPruneResult NotFound { get; } = new() { Success = false, Failure = SyncSessionPruneFailure.NotFound };

    public static SyncSessionPruneResult Succeeded(int deletedCount) => new() { Success = true, DeletedCount = deletedCount };
}

/// <summary>
/// Prunes old/completed <see cref="DeviceSyncSession"/> entities for a device owned by the current
/// user, along with their <see cref="DeviceSyncSessionRecord"/> rows and staging directories.
/// Reuses <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> for the identity check and
/// <see cref="StagingDirectoryCleanupService"/> for staging cleanup.
/// </summary>
public interface ISyncSessionPruneService
{
    /// <summary>
    /// Prunes sessions for device <paramref name="deviceId"/> owned by <paramref name="ownerId"/>.
    /// When <paramref name="all"/> is <c>true</c> every non-protected session is removed; otherwise
    /// sessions older than one day or beyond the 10 most recent are removed. Recent in-progress
    /// sessions are always protected.
    /// </summary>
    Task<SyncSessionPruneResult> PruneAsync(
        long deviceId,
        long ownerId,
        bool all,
        CancellationToken cancellationToken);
}