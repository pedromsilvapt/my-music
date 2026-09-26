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

        // A failed action is acknowledged so the commit is not blocked by it; the Error record
        // links to it so the commit does not apply it either
        if (input.RecordId.HasValue)
        {
            var failedRecord = await db.DeviceSyncSessionRecords
                .FirstOrDefaultAsync(r => r.Id == input.RecordId.Value && r.SessionId == session.Id, cancellationToken);
            if (failedRecord == null) return SyncReportErrorResult.RecordNotFound;
            if (!failedRecord.Action.IsClientAction()) return SyncReportErrorResult.RecordNotClientAction;

            failedRecord.Acknowledged = true;
        }

        var syncActions = syncActionsServerFactory.Create(db, sessionId, deviceId, session.IsDryRun);
        var record = await syncActions.ActionError(
            input.FilePath,
            input.ErrorMessage,
            input.SongId,
            reason: input.ErrorMessage,
            failedRecordId: input.RecordId,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Recorded sync error for device {DeviceId} session {SessionId}: {Path} ({ErrorMessage})",
            deviceId, sessionId, input.FilePath, input.ErrorMessage);

        return SyncReportErrorResult.Succeeded(record);
    }
}
