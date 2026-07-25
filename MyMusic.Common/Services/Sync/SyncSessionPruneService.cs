using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

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
/// Extracted from DevicesController.PruneSessions. Reuses <see cref="IDeviceLookupService"/> for
/// the identity check and <see cref="StagingDirectoryCleanupService"/> for staging cleanup.
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

/// <summary>
/// Default implementation of <see cref="ISyncSessionPruneService"/>.
/// </summary>
public class SyncSessionPruneService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    IFileSystem fileSystem,
    ILogger<SyncSessionPruneService> logger) : ISyncSessionPruneService
{
    /// <summary>
    /// Mirrors the original controller guard: sessions in progress within the last few seconds
    /// cannot be pruned (likely an accidental prune while a sync is running).
    /// </summary>
    public const int InProgressSafetyThresholdSeconds = 10;

    private const int KeepRecentCount = 10;
    private const int OldSessionAgeDays = 1;

    /// <inheritdoc />
    public async Task<SyncSessionPruneResult> PruneAsync(
        long deviceId,
        long ownerId,
        bool all,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return SyncSessionPruneResult.NotFound;

        var allSessions = await db.DeviceSyncSessions
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        var cutoffDate = DateTime.UtcNow.AddDays(-OldSessionAgeDays);

        DateTime? keepThreshold = null;
        if (!all && allSessions.Count > KeepRecentCount)
        {
            keepThreshold = allSessions[KeepRecentCount - 1].StartedAt;
        }

        var sessionsToDelete = allSessions.Where(s =>
        {
            if (s.Status == SyncSessionStatus.InProgress &&
                s.StartedAt > DateTime.UtcNow.AddSeconds(-InProgressSafetyThresholdSeconds))
            {
                return false;
            }

            if (all)
            {
                return true;
            }

            var olderThanOneDay = s.StartedAt < cutoffDate;
            var olderThanThreshold = keepThreshold.HasValue && s.StartedAt < keepThreshold.Value;

            return olderThanOneDay || olderThanThreshold;
        }).ToList();

        var sessionIds = sessionsToDelete.Select(s => s.Id).ToList();

        foreach (var session in sessionsToDelete)
        {
            StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);
        }

        await db.DeviceSyncSessionRecords
            .Where(r => sessionIds.Contains(r.SessionId))
            .ExecuteDeleteAsync(cancellationToken);

        var sessionsDeleted = await db.DeviceSyncSessions
            .Where(s => sessionIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Pruned {DeletedCount} sync sessions and records for device {DeviceId}", sessionsDeleted, deviceId);

        return SyncSessionPruneResult.Succeeded(sessionsDeleted);
    }
}