using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

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