using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncPendingActionsService"/>. Absorbs the former
/// <c>DevicesController.CreatePendingActionsForDevice</c> helper and the path-naming delegation that
/// previously lived in <c>DevicesController.ComputePendingActionPath</c>.
/// </summary>
public class SyncPendingActionsService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncSessionLookupService sessionLookup,
    ISyncPathResolver pathResolver,
    IOptions<Config> config,
    ILogger<SyncPendingActionsService> logger) : ISyncPendingActionsService
{
    /// <inheritdoc />
    public async Task<SyncPendingActionsResult?> CreateAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return null;

        var session = await sessionLookup.FindSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);

        var records = await CreatePendingActionsForDevice(deviceId, session?.NamingTemplate ?? device.NamingTemplate, sessionId, cancellationToken);

        logger.LogInformation("Created {Count} pending action records for device {DeviceId}", records.Count, deviceId);

        return new SyncPendingActionsResult { Records = records };
    }

    private async Task<List<DeviceSyncSessionRecord>> CreatePendingActionsForDevice(
        long deviceId,
        string? namingTemplate,
        long sessionId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CreatePendingActionsForDevice: DeviceId={DeviceId}, Template={NamingTemplate}, Default={DefaultNamingTemplate}",
            deviceId, namingTemplate ?? "(null)", config.Value.DefaultNamingTemplate);

        // A SongDevice is skipped only when the session already has a record at its path. Records of the same
        // song at other paths must not hide its pending action; a Link at another path turns a pending download
        // into an Unlink below
        var songDevices = await db.SongDevices
            .IncludeSongMetadata("Song")
            .Where(sd => sd.DeviceId == deviceId
                && sd.SyncAction != null
                && sd.SyncAction != SongSyncAction.Upload
                && !db.DeviceSyncSessionRecords.Any(r => r.SessionId == sessionId && r.FilePath == sd.DevicePath))
            .ToListAsync(cancellationToken);

        // Songs linked by checksum to a local file in this session: the device already holds their content
        var linkedPaths = (await db.DeviceSyncSessionRecords
                .Where(r => r.SessionId == sessionId && r.Action == SyncRecordAction.Link && r.SongId != null)
                .OrderBy(r => r.Id)
                .Select(r => new { SongId = r.SongId!.Value, r.FilePath })
                .ToListAsync(cancellationToken))
            .DistinctBy(r => r.SongId)
            .ToDictionary(r => r.SongId, r => r.FilePath);

        var allExistingPaths = await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .Select(sd => sd.DevicePath)
            .ToHashSetAsync(cancellationToken);

        var namingStrategy = new TemplateNamingStrategy(
            namingTemplate ?? config.Value.DefaultNamingTemplate);

        var usedPaths = new HashSet<string>(allExistingPaths);
        var createdRecords = new List<DeviceSyncSessionRecord>();

        foreach (var sd in songDevices)
        {
            if (sd.SyncAction == SongSyncAction.Remove)
            {
                var record = DeviceSyncSessionRecordForAction(sessionId, SyncRecordAction.DeleteLocal, sd.DevicePath, sd.SongId, sd.SyncActionReason);
                createdRecords.Add(record);
            }
            else if (sd.SyncAction == SongSyncAction.Download)
            {
                if (sd.SongId is { } songId && linkedPaths.TryGetValue(songId, out var linkedPath) && linkedPath != sd.DevicePath)
                {
                    var unlinkRecord = DeviceSyncSessionRecordForAction(sessionId, SyncRecordAction.Unlink, sd.DevicePath, sd.SongId,
                        $"Replaced by linked file '{linkedPath}'");
                    createdRecords.Add(unlinkRecord);
                    continue;
                }

                var (path, previousPath) = pathResolver.ComputePendingActionPath(sd, namingStrategy, usedPaths);
                usedPaths.Add(path);

                var isUpdate = sd.LastSyncedModifiedAt != null;
                var action = isUpdate ? SyncRecordAction.UpdateLocal : SyncRecordAction.CreateLocal;

                var modifiedAt = sd.Song?.ModifiedAt;
                JsonElement? data = modifiedAt.HasValue
                    ? SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = sd.SongId, ModifiedAt = modifiedAt })
                    : null;
                var updateFilePath = (previousPath != null && action == SyncRecordAction.UpdateLocal) ? previousPath : path;
                var record = new DeviceSyncSessionRecord
                {
                    SessionId = sessionId,
                    FilePath = updateFilePath,
                    Action = action,
                    Data = data,
                    SongId = sd.SongId,
                    Reason = sd.SyncActionReason,
                    Acknowledged = false,
                    ProcessedAt = DateTime.UtcNow,
                };
                createdRecords.Add(record);

                if (previousPath != null && action == SyncRecordAction.UpdateLocal)
                {
                    var renameData = SyncActionDataSerializer.Serialize(new RenameData
                    {
                        PreviousPath = previousPath,
                        NewPath = path,
                    });
                    var renameRecord = new DeviceSyncSessionRecord
                    {
                        SessionId = sessionId,
                        FilePath = path,
                        Action = SyncRecordAction.Rename,
                        Data = renameData,
                        SongId = sd.SongId,
                        Reason = sd.SyncActionReason,
                        Acknowledged = false,
                        ProcessedAt = DateTime.UtcNow,
                    };
                    createdRecords.Add(renameRecord);
                }

                logger.LogInformation(
                    "CreatePendingActionsForDevice: SongId={SongId}, Title='{Title}', DevicePath='{DevicePath}', newPath='{NewPath}', Action={Action}, SamePath={SamePath}",
                    sd.SongId, sd.Song?.Title, sd.DevicePath, path, action, path == sd.DevicePath);
            }
        }

        if (createdRecords.Count > 0)
        {
            db.DeviceSyncSessionRecords.AddRange(createdRecords);
            await db.SaveChangesAsync(cancellationToken);
        }

        return createdRecords;
    }

    private static DeviceSyncSessionRecord DeviceSyncSessionRecordForAction(long sessionId, SyncRecordAction action, string filePath, long? songId, string? reason)
    {
        return new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = filePath,
            Action = action,
            SongId = songId,
            Reason = reason,
            Acknowledged = false,
            ProcessedAt = DateTime.UtcNow,
        };
    }
}