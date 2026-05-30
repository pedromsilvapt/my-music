using System.Text.Json;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public class SyncActionsServer(
    MusicDbContext db,
    long sessionId) : ISyncActionsServer
{
    public async Task<DeviceSyncSessionRecord> ActionCreateRemote(
        string filePath, long? songId, string checksum, string algorithm, DateTime modifiedAt,
        string? tempFilePath = null, DateTime? createdAt = null, string? originalFilePath = null,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var data = new CreateRemoteData
        {
            SongId = songId,
            Checksum = checksum,
            Algorithm = algorithm,
            ModifiedAt = modifiedAt,
            TempFilePath = tempFilePath,
            CreatedAt = createdAt,
            OriginalFilePath = originalFilePath,
        };
        var dataElement = SyncActionDataSerializer.Serialize(data);
        var record = CreateRecord(filePath, SyncRecordAction.CreateRemote, dataElement, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionUpdateRemote(
        string filePath, long? songId, string checksum, string algorithm, DateTime modifiedAt,
        string? tempFilePath = null, DateTime? createdAt = null, string? originalFilePath = null,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var data = new UpdateRemoteData
        {
            SongId = songId,
            Checksum = checksum,
            Algorithm = algorithm,
            ModifiedAt = modifiedAt,
            TempFilePath = tempFilePath,
            CreatedAt = createdAt,
            OriginalFilePath = originalFilePath,
        };
        var dataElement = SyncActionDataSerializer.Serialize(data);
        var record = CreateRecord(filePath, SyncRecordAction.UpdateRemote, dataElement, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionCreateLocal(
        string filePath, long? songId = null, DateTime? modifiedAt = null,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var data = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = songId, ModifiedAt = modifiedAt });
        var record = CreateRecord(filePath, SyncRecordAction.CreateLocal, data, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionUpdateLocal(
        string filePath, long? songId = null, DateTime? modifiedAt = null,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var data = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = songId, ModifiedAt = modifiedAt });
        var record = CreateRecord(filePath, SyncRecordAction.UpdateLocal, data, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionDelete(
        string filePath, long? songId = null, string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var data = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = songId });
        var record = CreateRecord(filePath, SyncRecordAction.Delete, data, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionLink(
        string filePath, long songId, DateTime? modifiedAt = null,
        string? checksum = null, string? algorithm = null, string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var data = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = songId, ModifiedAt = modifiedAt, Checksum = checksum, Algorithm = algorithm });
        var record = CreateRecord(filePath, SyncRecordAction.Link, data, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionLink(
        string filePath, string checksum, string algorithm, DateTime modifiedAt,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var data = new SongModifiedAtData
        {
            Checksum = checksum,
            Algorithm = algorithm,
            ModifiedAt = modifiedAt,
        };
        var dataElement = SyncActionDataSerializer.Serialize(data);
        var record = CreateRecord(filePath, SyncRecordAction.Link, dataElement, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionUnlink(
        string filePath, long? songId = null, string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var data = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = songId });
        var record = CreateRecord(filePath, SyncRecordAction.Unlink, data, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionRename(
        string filePath, string previousPath, string newPath, long? songId = null,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var data = new RenameData { PreviousPath = previousPath, NewPath = newPath };
        var dataElement = SyncActionDataSerializer.Serialize(data);
        var record = CreateRecord(filePath, SyncRecordAction.Rename, dataElement, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionSkipped(
        string filePath, long? songId = null, DateTime? modifiedAt = null,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        JsonElement? data = modifiedAt.HasValue
            ? SyncActionDataSerializer.Serialize(new SongModifiedAtData { ModifiedAt = modifiedAt })
            : null;
        var record = CreateRecord(filePath, SyncRecordAction.Skipped, data, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionConflict(
        string filePath, DateTime localModifiedAt, DateTime serverModifiedAt, long? songId = null,
        string? reason = null, string? localChecksum = null, string? serverChecksum = null,
        string? algorithm = null, CancellationToken cancellationToken = default)
    {
        var data = new ConflictData
        {
            LocalModifiedAt = localModifiedAt,
            ServerModifiedAt = serverModifiedAt,
            LocalChecksum = localChecksum,
            ServerChecksum = serverChecksum,
            Algorithm = algorithm,
        };
        var dataElement = SyncActionDataSerializer.Serialize(data);
        var record = CreateRecord(filePath, SyncRecordAction.Conflict, dataElement, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionUpdateTimestamp(
        string filePath, DateTime newTimestamp, long? songId = null,
        string? reason = null, DateTime? modifiedAt = null, DateTime? createdAt = null,
        string? originalFilePath = null, CancellationToken cancellationToken = default)
    {
        var data = new UpdateTimestampData
        {
            NewTimestamp = newTimestamp,
            SongId = songId,
            ModifiedAt = modifiedAt,
            CreatedAt = createdAt,
            OriginalFilePath = originalFilePath,
        };
        var dataElement = SyncActionDataSerializer.Serialize(data);
        var record = CreateRecord(filePath, SyncRecordAction.UpdateTimestamp, dataElement, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    public async Task<DeviceSyncSessionRecord> ActionError(
        string filePath, string errorMessage, long? songId = null,
        string? reason = null, CancellationToken cancellationToken = default)
    {
        var data = SyncActionDataSerializer.Serialize(new ErrorData { ErrorMessage = errorMessage });
        var record = CreateRecord(filePath, SyncRecordAction.Error, data, songId, reason: reason);
        return await SaveRecord(record, cancellationToken);
    }

    private DeviceSyncSessionRecord CreateRecord(
        string filePath, SyncRecordAction action, JsonElement? data = null, long? songId = null,
        bool acknowledged = false, string? reason = null)
    {
        return new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = filePath,
            Action = action,
            Data = data,
            SongId = songId,
            Reason = reason,
            Acknowledged = acknowledged,
            ProcessedAt = DateTime.UtcNow,
        };
    }

    private async Task<DeviceSyncSessionRecord> SaveRecord(
        DeviceSyncSessionRecord record, CancellationToken cancellationToken)
    {
        db.DeviceSyncSessionRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return record;
    }
}