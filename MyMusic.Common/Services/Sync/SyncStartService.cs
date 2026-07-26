using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncStartService"/>.
/// </summary>
public class SyncStartService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncActionsServerFactory syncActionsServerFactory,
    ILogger<SyncStartService> logger) : ISyncStartService
{
    /// <inheritdoc />
    public async Task<SyncStartResult?> StartAsync(
        long deviceId,
        long ownerId,
        SyncStartInput input,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return null;

        var session = new DeviceSyncSession
        {
            DeviceId = deviceId,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = input.DryRun,
            RepositoryPath = input.RepositoryPath,
        };

        db.DeviceSyncSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        if (input.ScanErrors is { Count: > 0 })
        {
            var syncActions = syncActionsServerFactory.Create(db, session.Id, deviceId, session.IsDryRun);
            foreach (var error in input.ScanErrors)
            {
                await syncActions.ActionError(error.FilePath, error.ErrorMessage, reason: $"Scan error: {error.ErrorMessage}", cancellationToken: cancellationToken);
            }
        }

        logger.LogInformation(
            "Started sync session {SessionId} for device {DeviceId} (DryRun: {IsDryRun}, RepositoryPath: {RepositoryPath})",
            session.Id, deviceId, session.IsDryRun, session.RepositoryPath);

        return new SyncStartResult { SessionId = session.Id };
    }
}