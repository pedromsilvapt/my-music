using MyMusic.Common.Entities;
using MyMusic.Common.NamingStrategies;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Creates the records that make a device download the current file of a song it already holds.
/// </summary>
public static class SyncUpdateLocalRecords
{
    /// <summary>
    /// Adds an <c>UpdateLocal</c> record that downloads the song's current file to the device, followed
    /// by a <c>Rename</c> record when the naming template changed the file's target path. The target path
    /// is added to <paramref name="usedPaths"/>. Returns the created records, in that order.
    /// </summary>
    public static async Task<List<DeviceSyncSessionRecord>> AddAsync(
        ISyncPathResolver pathResolver,
        SongDevice songDevice,
        long songId,
        string reason,
        TemplateNamingStrategy namingStrategy,
        HashSet<string> usedPaths,
        ISyncActionsServer syncActions,
        CancellationToken cancellationToken)
    {
        var records = new List<DeviceSyncSessionRecord>();

        var pendingAction = pathResolver.ComputePendingActionPath(songDevice, namingStrategy, usedPaths);
        usedPaths.Add(pendingAction.Path);

        var updateFilePath = pendingAction.PreviousPath ?? pendingAction.Path;
        var songFileModifiedAt = songDevice.Song!.FileModifiedAt ?? songDevice.Song.ModifiedAt;
        records.Add(await syncActions.ActionUpdateLocal(updateFilePath, songId, songFileModifiedAt, reason, cancellationToken));

        if (pendingAction.PreviousPath != null)
        {
            records.Add(await syncActions.ActionRename(pendingAction.Path, pendingAction.PreviousPath, pendingAction.Path, songId, "Path updated by naming template", cancellationToken));
        }

        return records;
    }
}
