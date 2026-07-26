using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncAcknowledgeService"/>.
/// </summary>
public class SyncAcknowledgeService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncCommitService syncCommitService,
    ILogger<SyncAcknowledgeService> logger) : ISyncAcknowledgeService
{
    /// <inheritdoc />
    public async Task<SyncAcknowledgeResult> AcknowledgeAsync(
        long deviceId,
        long ownerId,
        SyncAcknowledgeInput input,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return SyncAcknowledgeResult.DeviceNotFound;

        if (input.RecordIds is not { Count: > 0 })
        {
            return SyncAcknowledgeResult.BadRequestResult;
        }

        var records = await db.DeviceSyncSessionRecords
            .Where(r => input.RecordIds.Contains(r.Id) && r.Session.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        await syncCommitService.AcknowledgeRecordsAsync(records, input.ModifiedAt);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Acknowledged {Count} records for device {DeviceId}", records.Count, deviceId);

        return SyncAcknowledgeResult.Succeeded(records);
    }
}