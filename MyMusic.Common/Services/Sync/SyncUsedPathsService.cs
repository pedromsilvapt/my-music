using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncUsedPathsService"/>. Scoped: the loaded paths are cached
/// for the request.
/// </summary>
public class SyncUsedPathsService : ISyncUsedPathsService
{
    private readonly Dictionary<(long DeviceId, long SessionId), SyncUsedPaths> _loaded = [];

    /// <inheritdoc />
    public async Task<SyncUsedPaths> GetAsync(MusicDbContext db, long deviceId, long sessionId, CancellationToken cancellationToken)
    {
        if (_loaded.TryGetValue((deviceId, sessionId), out var loaded))
        {
            return loaded;
        }

        var usedPaths = new SyncUsedPaths(await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .Select(sd => sd.DevicePath)
            .ToListAsync(cancellationToken));

        // Device paths only change at commit, so the paths taken and freed earlier in the session are
        // only in its records
        var records = await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId
                        && (r.Action == SyncRecordAction.Rename
                            || r.Action == SyncRecordAction.CreateLocal
                            || r.Action == SyncRecordAction.DeleteLocal))
            .OrderBy(r => r.Id)
            .Select(r => new { r.Action, r.FilePath, r.Data })
            .ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            if (record.Action == SyncRecordAction.Rename)
            {
                var data = SyncActionDataSerializer.Deserialize<RenameData>(record.Data);
                if (data?.PreviousPath != null)
                {
                    usedPaths.Free(data.PreviousPath);
                }

                usedPaths.Reserve(data?.NewPath ?? record.FilePath);
            }
            else if (record.Action == SyncRecordAction.CreateLocal)
            {
                usedPaths.Reserve(record.FilePath);
            }
            else
            {
                usedPaths.Free(record.FilePath);
            }
        }

        _loaded[(deviceId, sessionId)] = usedPaths;

        return usedPaths;
    }
}
