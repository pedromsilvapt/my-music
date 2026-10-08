using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public interface ISyncActionsServer
{
    Task<DeviceSyncSessionRecord> ActionCreateRemote(string filePath, long? songId, string checksum, string algorithm, DateTime modifiedAt, string? tempFilePath = null, DateTime? createdAt = null, string? originalFilePath = null, string? reason = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionUpdateRemote(string filePath, long? songId, string checksum, string algorithm, DateTime modifiedAt, string? tempFilePath = null, DateTime? createdAt = null, string? originalFilePath = null, string? reason = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Records the download of a song to the device. <paramref name="modifiedAt"/> is when the song's file last
    /// changed and <paramref name="createdAt"/> when the song was created: the device can give the file either date.
    /// </summary>
    Task<DeviceSyncSessionRecord> ActionCreateLocal(string filePath, long? songId = null, DateTime? modifiedAt = null, DateTime? createdAt = null, string? reason = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Records the download of a song to the device. <paramref name="modifiedAt"/> is when the song's file last
    /// changed and <paramref name="createdAt"/> when the song was created: the device can give the file either date.
    /// </summary>
    Task<DeviceSyncSessionRecord> ActionUpdateLocal(string filePath, long? songId = null, DateTime? modifiedAt = null, DateTime? createdAt = null, string? reason = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionDeleteLocal(string filePath, long? songId = null, string? reason = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionLink(string filePath, long songId, DateTime? modifiedAt = null, string? checksum = null, string? algorithm = null, string? reason = null, bool isPreviousVersion = false, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionLink(string filePath, string checksum, string algorithm, DateTime modifiedAt, string? reason = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Records a soundalike <c>Link</c>: to library song <paramref name="songId"/>, or, when it is null, to the
    /// song the file uploaded earlier in this session with <paramref name="checksum"/> becomes at commit.
    /// </summary>
    Task<DeviceSyncSessionRecord> ActionSoundalikeLink(string filePath, long? songId, string checksum, string algorithm, string localChecksum, DateTime modifiedAt, string? reason = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Records an <c>UpdateLocal</c> the device performs by copying its own file at <paramref name="localSourcePath"/>
    /// over <paramref name="filePath"/>, instead of downloading it from the server.
    /// </summary>
    Task<DeviceSyncSessionRecord> ActionUpdateLocalFromLocalFile(string filePath, string localSourcePath, string? reason = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionUnlink(string filePath, long? songId = null, string? reason = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionRename(string filePath, string previousPath, string newPath, long? songId = null, string? reason = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionSkipped(string filePath, long? songId = null, string? reason = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Adds a <c>Skipped</c> record without saving it: the caller saves once for a whole batch of files
    /// (most files of a sync are skipped, so one save per file is the bulk of its cost).
    /// </summary>
    DeviceSyncSessionRecord ActionSkippedDeferred(string filePath, long? songId = null, string? reason = null);
    Task<DeviceSyncSessionRecord> ActionConflict(string filePath, DateTime localModifiedAt, DateTime serverModifiedAt, long? songId = null, string? reason = null, string? localChecksum = null, string? serverChecksum = null, string? algorithm = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionUpdateTimestamp(string filePath, DateTime newTimestamp, long? songId = null, string? reason = null, DateTime? modifiedAt = null, DateTime? createdAt = null, string? originalFilePath = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionError(string filePath, string errorMessage, long? songId = null, string? reason = null, long? failedRecordId = null, CancellationToken cancellationToken = default);
}