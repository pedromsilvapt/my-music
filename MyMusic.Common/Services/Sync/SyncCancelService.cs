using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Result of a sync session cancel operation. The controller maps this to
/// <c>SyncCancelResponse</c>.
/// </summary>
public record SyncCancelResult
{
    public required bool StagingDirectoryDeleted { get; init; }
}

/// <summary>
/// Cancels an in-progress <see cref="DeviceSyncSession"/> for a device owned by the current
/// user, marking the session as <see cref="SyncSessionStatus.Cancelled"/> and deleting the
/// session's staging directory. Extracted from DevicesController.CancelSync as part of Phase 9
/// of the controllers refactor so the controller stays thin (input/output + DTO mapping only).
/// Reuses <see cref="ISyncSessionLookupService"/> for the session identity check and
/// <see cref="StagingDirectoryCleanupService"/> for staging cleanup.
/// </summary>
public interface ISyncCancelService
{
    /// <summary>
    /// Cancels the session <paramref name="sessionId"/> scoped to <paramref name="deviceId"/>
    /// owned by <paramref name="ownerId"/>. Throws when the session is not in the
    /// <see cref="SyncSessionStatus.InProgress"/> state, mirroring the previous controller
    /// behavior. Returns <c>null</c> when no such session exists (NotFound).
    /// </summary>
    Task<SyncCancelResult?> CancelAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken);
}

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