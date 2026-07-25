using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

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
/// <see cref="DeviceSyncSessionRecord"/> rows and staging directory. Extracted from
/// DevicesController.DeleteSession. Reuses <see cref="ISyncSessionLookupService"/> for the
/// identity check and <see cref="StagingDirectoryCleanupService"/> for staging cleanup.
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

/// <summary>
/// Default implementation of <see cref="ISyncSessionDeleteService"/>.
/// </summary>
public class SyncSessionDeleteService(
    MusicDbContext db,
    ISyncSessionLookupService sessionLookup,
    IFileSystem fileSystem,
    ILogger<SyncSessionDeleteService> logger) : ISyncSessionDeleteService
{
    /// <summary>
    /// Mirrors the original controller guard: sessions in progress within the last few seconds
    /// cannot be deleted (likely an accidental delete while a sync is running).
    /// </summary>
    public const int InProgressSafetyThresholdSeconds = 10;

    /// <inheritdoc />
    public async Task<SyncSessionDeleteResult> DeleteAsync(
        long sessionId,
        long deviceId,
        long ownerId,
        CancellationToken cancellationToken)
    {
        var session = await sessionLookup.FindSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (session == null) return SyncSessionDeleteResult.Failed(SyncSessionDeleteFailure.NotFound);

        if (session.Status == SyncSessionStatus.InProgress &&
            session.StartedAt > DateTime.UtcNow.AddSeconds(-InProgressSafetyThresholdSeconds))
        {
            return SyncSessionDeleteResult.Failed(SyncSessionDeleteFailure.InProgress);
        }

        StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);

        var recordsDeleted = await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        await db.DeviceSyncSessions
            .Where(s => s.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Deleted sync session {SessionId} and {RecordCount} records", sessionId, recordsDeleted);

        return SyncSessionDeleteResult.Succeeded;
    }
}