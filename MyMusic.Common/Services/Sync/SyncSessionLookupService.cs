using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncSessionLookupService"/>.
/// </summary>
public class SyncSessionLookupService : ISyncSessionLookupService
{
    /// <inheritdoc />
    public async Task<DeviceSyncSession?> FindSessionAsync(MusicDbContext db, long sessionId, long deviceId, long ownerId, CancellationToken cancellationToken)
    {
        return await db.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == ownerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ActiveSessionResult> GetActiveSessionAsync(MusicDbContext db, long sessionId, long deviceId, long ownerId, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (session == null) return ActiveSessionResult.NotFound;
        if (session.Status != SyncSessionStatus.InProgress)
            return ActiveSessionResult.NotInProgress(sessionId, session.Status);
        return ActiveSessionResult.Succeeded(session);
    }
}