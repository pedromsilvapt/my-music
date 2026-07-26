using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncCancelService"/>.
/// </summary>
public class SyncCancelService(
    MusicDbContext db,
    ISyncSessionLookupService sessionLookup,
    IFileSystem fileSystem,
    ILogger<SyncCancelService> logger) : ISyncCancelService
{
    /// <inheritdoc />
    public async Task<SyncCancelResult?> CancelAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken)
    {
        var session = await sessionLookup.FindSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (session == null) return null;

        if (session.Status != SyncSessionStatus.InProgress)
        {
            throw new Exception($"Sync session {sessionId} cannot be cancelled (status: {session.Status})");
        }

        session.Status = SyncSessionStatus.Cancelled;
        session.CompletedAt = DateTime.UtcNow;

        var stagingDeleted = StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cancelled sync session {SessionId} for device {DeviceId}", sessionId, deviceId);

        return new SyncCancelResult { StagingDirectoryDeleted = stagingDeleted };
    }
}