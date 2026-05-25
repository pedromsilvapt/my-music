using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public interface ISyncActionsServer
{
    Task<DeviceSyncSessionRecord> ActionCreateRemote(string filePath, long? songId, string checksum, string algorithm, DateTime modifiedAt, string? tempFilePath = null, DateTime? createdAt = null, string? originalFilePath = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionUpdateRemote(string filePath, long? songId, string checksum, string algorithm, DateTime modifiedAt, string? tempFilePath = null, DateTime? createdAt = null, string? originalFilePath = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionCreateLocal(string filePath, long? songId = null, DateTime? modifiedAt = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionUpdateLocal(string filePath, long? songId = null, DateTime? modifiedAt = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionDelete(string filePath, long? songId = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionLink(string filePath, long songId, DateTime? modifiedAt = null, string? checksum = null, string? algorithm = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionLink(string filePath, string checksum, string algorithm, DateTime modifiedAt, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionUnlink(string filePath, long? songId = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionRename(string filePath, string previousPath, string newPath, long? songId = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionSkipped(string filePath, long? songId = null, DateTime? modifiedAt = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionConflict(string filePath, DateTime localModifiedAt, DateTime serverModifiedAt, long? songId = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionUpdateTimestamp(string filePath, DateTime newTimestamp, long? songId = null, CancellationToken cancellationToken = default);
    Task<DeviceSyncSessionRecord> ActionError(string filePath, string errorMessage, long? songId = null, CancellationToken cancellationToken = default);
}