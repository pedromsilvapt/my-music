using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncReportErrorService"/>.
/// </summary>
public class SyncReportErrorService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncSessionLookupService sessionLookup,
    ISyncActionsServerFactory syncActionsServerFactory,
    ILogger<SyncReportErrorService> logger) : ISyncReportErrorService
{
    /// <inheritdoc />
    public async Task<SyncReportErrorResult> ReportErrorAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        SyncReportErrorInput input,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return SyncReportErrorResult.DeviceNotFound;

        var session = await sessionLookup.FindSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (session == null) return SyncReportErrorResult.SessionNotFound(sessionId);

        var syncActions = syncActionsServerFactory.Create(db, sessionId, deviceId, session.IsDryRun);
        var record = await syncActions.ActionError(
            input.FilePath,
            input.ErrorMessage,
            input.SongId,
            reason: input.ErrorMessage,
            cancellationToken);

        logger.LogInformation(
            "Recorded sync error for device {DeviceId} session {SessionId}: {Path} ({ErrorMessage})",
            deviceId, sessionId, input.FilePath, input.ErrorMessage);

        return SyncReportErrorResult.Succeeded(record);
    }
}