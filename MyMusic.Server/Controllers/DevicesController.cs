using System.Globalization;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Filters;
using MyMusic.Common.Models;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Devices;
using MyMusic.Server.DTO.Filters;
using MyMusic.Server.DTO.Sync;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("devices")]
public class DevicesController(
    ILogger<DevicesController> logger,
    ICurrentUser currentUser,
    MusicDbContext context,
    IMusicService musicService,
    IConfiguration configuration,
    ILogger<MusicImportJob> importJobLogger,
    IFileSystem fileSystem) : ControllerBase
{
    [HttpGet]
    public async Task<ListDevicesResponse> List(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null)
    {
        var query = context.Devices
            .Where(d => d.OwnerId == currentUser.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = FuzzySearchHelper.ApplyFuzzySearch(query, search, d => d.SearchableText);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterRequest = FilterDslParser.Parse(filter);
            var filterExpression = DynamicFilterBuilder.BuildFilter<Device>(filterRequest);
            query = query.Where(filterExpression);
        }

        var devices = await query.ToListAsync(cancellationToken);

        var songCounts = await context.SongDevices
            .GroupBy(sd => sd.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeviceId, x => x.Count, cancellationToken);

        return new ListDevicesResponse
        {
            Devices = devices.Select(d => ListDeviceItem.FromEntity(d, songCounts.GetValueOrDefault(d.Id, 0))).ToList(),
        };
    }

    [HttpPost]
    public async Task<CreateDeviceResponse> Create([FromBody] CreateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken)
                   ?? throw new Exception("User not found");

        var device = new Device
        {
            Name = request.Name,
            Icon = request.Icon,
            Color = request.Color,
            NamingTemplate = request.NamingTemplate,
            ImportOnPurchase = request.ImportOnPurchase,
            OwnerId = currentUser.Id,
            Owner = user,
        };

        context.Devices.Add(device);
        await context.SaveChangesAsync(cancellationToken);

        if (request.ForceCreateActions)
        {
            var existingSongDevices = await context.SongDevices
                .Where(sd => sd.DeviceId == device.Id)
                .ToListAsync(cancellationToken);
            foreach (var sd in existingSongDevices)
            {
                sd.SyncAction = SongSyncAction.Download;
            }
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Set ForceCreateActions for {Count} SongDevices on device {DeviceId}",
                existingSongDevices.Count, device.Id);
        }

        logger.LogInformation("Created device {DeviceName} with ID {DeviceId} for user {UserId}",
            device.Name, device.Id, currentUser.Id);

        return new CreateDeviceResponse
        {
            Device = CreateDeviceItem.FromEntity(device),
        };
    }

    [HttpPut("{deviceId:long}")]
    public async Task<UpdateDeviceResponse> Update(long deviceId, [FromBody] UpdateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        device.Icon = request.Icon;
        device.Color = request.Color;
        device.NamingTemplate = request.NamingTemplate;
        if (request.ImportOnPurchase.HasValue)
        {
            device.ImportOnPurchase = request.ImportOnPurchase.Value;
        }

        if (request.ForceCreateActions == true)
        {
            var existingSongDevices = await context.SongDevices
                .Where(sd => sd.DeviceId == deviceId)
                .ToListAsync(cancellationToken);
            foreach (var sd in existingSongDevices)
            {
                sd.SyncAction = SongSyncAction.Download;
            }
            logger.LogInformation("Set ForceCreateActions for {Count} SongDevices on device {DeviceId}",
                existingSongDevices.Count, deviceId);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated device {DeviceId} for user {UserId}", deviceId, currentUser.Id);

        return new UpdateDeviceResponse
        {
            Device = new UpdateDeviceItem
            {
                Id = device.Id,
                Name = device.Name,
                Icon = device.Icon,
                Color = device.Color,
                NamingTemplate = device.NamingTemplate,
                ImportOnPurchase = device.ImportOnPurchase,
            },
        };
    }

    [HttpDelete("{deviceId:long}")]
    public async Task<IActionResult> Delete(long deviceId, CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var songDevices = await context.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .ToListAsync(cancellationToken);
        context.SongDevices.RemoveRange(songDevices);

        var sessions = await context.DeviceSyncSessions
            .Where(s => s.DeviceId == deviceId)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            var records = await context.DeviceSyncSessionRecords
                .Where(r => r.SessionId == session.Id)
                .ToListAsync(cancellationToken);
            context.DeviceSyncSessionRecords.RemoveRange(records);
        }

        context.DeviceSyncSessions.RemoveRange(sessions);

        context.Devices.Remove(device);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted device {DeviceId} for user {UserId}", deviceId, currentUser.Id);

        return NoContent();
    }

    [HttpGet("{deviceId:long}/sessions")]
    public async Task<ListSyncSessionsResponse> ListSessions(
        long deviceId,
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var sessions = await context.DeviceSyncSessions
            .Include(s => s.Records)
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return new ListSyncSessionsResponse
        {
            Sessions = sessions.Select(SyncSessionItem.FromEntity).ToList(),
        };
    }

    private const int InProgressSafetyThresholdSeconds = 10;

    [HttpDelete("{deviceId:long}/sessions/{sessionId:long}")]
    public async Task<DeleteSessionResponse> DeleteSession(
        long deviceId,
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new Exception($"Sync session not found with id {sessionId}");
        }

        if (session.Status == SyncSessionStatus.InProgress &&
            session.StartedAt > DateTime.UtcNow.AddSeconds(-InProgressSafetyThresholdSeconds))
        {
            throw new Exception("Cannot delete a session that is currently in progress");
        }

        var records = await context.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        context.DeviceSyncSessionRecords.RemoveRange(records);

        context.DeviceSyncSessions.Remove(session);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted sync session {SessionId} and {RecordCount} records", sessionId, records.Count);

        return new DeleteSessionResponse { Success = true };
    }

    [HttpPost("{deviceId:long}/sessions/prune")]
    public async Task<PruneSessionsResponse> PruneSessions(
        long deviceId,
        [FromBody] PruneSessionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var allSessions = await context.DeviceSyncSessions
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        var cutoffDate = DateTime.UtcNow.AddDays(-1);

        DateTime? keepThreshold = null;
        if (!request.All && allSessions.Count > 10)
        {
            keepThreshold = allSessions[9].StartedAt;
        }

        var sessionsToDelete = allSessions.Where(s =>
        {
            if (s.Status == SyncSessionStatus.InProgress &&
                s.StartedAt > DateTime.UtcNow.AddSeconds(-InProgressSafetyThresholdSeconds))
            {
                return false;
            }

            if (request.All)
            {
                return true;
            }

            var olderThanOneDay = s.StartedAt < cutoffDate;
            var olderThanThreshold = keepThreshold.HasValue && s.StartedAt < keepThreshold.Value;

            return olderThanOneDay || olderThanThreshold;
        }).ToList();

        var deletedCount = 0;
        foreach (var session in sessionsToDelete)
        {
            var records = await context.DeviceSyncSessionRecords
                .Where(r => r.SessionId == session.Id)
                .ToListAsync(cancellationToken);
            context.DeviceSyncSessionRecords.RemoveRange(records);

            context.DeviceSyncSessions.Remove(session);
            deletedCount++;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Pruned {DeletedCount} sync sessions for device {DeviceId}", deletedCount, deviceId);

        return new PruneSessionsResponse { DeletedCount = deletedCount };
    }

    [HttpGet("{deviceId:long}/sessions/{sessionId:long}/records")]
    public async Task<ListSyncRecordsResponse> GetSessionRecords(
        long deviceId,
        long sessionId,
        [FromQuery] string? actions = null,
        [FromQuery] string? source = null,
        CancellationToken cancellationToken = default)
    {
        var session = await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new Exception($"Sync session not found with id {sessionId}");
        }

        var query = context.DeviceSyncSessions
            .Where(s => s.Id == sessionId)
            .SelectMany(s => s.Records);

        if (!string.IsNullOrEmpty(actions))
        {
            var actionList = actions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(a => Enum.Parse<SyncRecordAction>(a, true))
                .ToHashSet();
            query = query.Where(r => actionList.Contains(r.Action));
        }

        if (!string.IsNullOrEmpty(source))
        {
            var sourceValue = Enum.Parse<SyncRecordSource>(source, true);
            query = query.Where(r => r.Source == sourceValue);
        }

        var records = await query
            .OrderBy(r => r.FilePath)
            .ToListAsync(cancellationToken);

        return new ListSyncRecordsResponse
        {
            Records = records.Select(SyncRecordResponseItem.FromEntity).ToList(),
        };
    }

    [HttpPost("{deviceId:long}/sync/start")]
    public async Task<SyncStartResponse> StartSync(long deviceId, [FromBody] SyncStartRequest? request,
        CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var session = new DeviceSyncSession
        {
            DeviceId = deviceId,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = request?.DryRun ?? false,
            RepositoryPath = request?.RepositoryPath,
        };

        context.DeviceSyncSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Started sync session {SessionId} for device {DeviceId} (DryRun: {IsDryRun}, RepositoryPath: {RepositoryPath})",
            session.Id, deviceId, session.IsDryRun, session.RepositoryPath);

        return new SyncStartResponse { SessionId = session.Id };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/records")]
    public async Task<SyncRecordsResponse> RecordChunk(long deviceId, long sessionId,
        [FromBody] SyncRecordsRequest request, CancellationToken cancellationToken)
    {
        var session = await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new Exception($"Sync session not found with id {sessionId}");
        }

        if (session.Status != SyncSessionStatus.InProgress)
        {
            throw new Exception($"Sync session {sessionId} is not in progress (status: {session.Status})");
        }

        var newRecordsCount = 0;
        var updatedRecordsCount = 0;

        foreach (var r in request.Records)
        {
            var existingRecord = await context.DeviceSyncSessionRecords
                .FirstOrDefaultAsync(rec => rec.SessionId == sessionId && rec.FilePath == r.FilePath,
                    cancellationToken);

            if (existingRecord != null)
            {
                existingRecord.Action = Enum.Parse<SyncRecordAction>(r.Action);
                existingRecord.SongId = r.SongId;
                existingRecord.ErrorMessage = r.ErrorMessage;
                existingRecord.Reason = r.Reason;
                existingRecord.Source = string.IsNullOrEmpty(r.Source)
                    ? SyncRecordSource.Device
                    : Enum.Parse<SyncRecordSource>(r.Source);
                existingRecord.ProcessedAt = DateTime.UtcNow;
                updatedRecordsCount++;
            }
            else
            {
                context.DeviceSyncSessionRecords.Add(new DeviceSyncSessionRecord
                {
                    SessionId = sessionId,
                    FilePath = r.FilePath,
                    Action = Enum.Parse<SyncRecordAction>(r.Action),
                    SongId = r.SongId,
                    ErrorMessage = r.ErrorMessage,
                    Reason = r.Reason,
                    Source = string.IsNullOrEmpty(r.Source)
                        ? SyncRecordSource.Device
                        : Enum.Parse<SyncRecordSource>(r.Source),
                    ProcessedAt = DateTime.UtcNow,
                });
                newRecordsCount++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Recorded {NewCount} new and {UpdatedCount} updated records for session {SessionId}",
            newRecordsCount, updatedRecordsCount, sessionId);

        return new SyncRecordsResponse { Success = true };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/complete")]
    public async Task<SyncCompleteResponse> CompleteSync(long deviceId, long sessionId,
        [FromBody] SyncCompleteRequest? request, CancellationToken cancellationToken)
    {
        var session = await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new Exception($"Sync session not found with id {sessionId}");
        }

        if (session.Status != SyncSessionStatus.InProgress)
        {
            throw new Exception($"Sync session {sessionId} is not in progress (status: {session.Status})");
        }

        var direction = request?.Direction?.ToLowerInvariant() ?? "both";
        var isUpDirection = direction == "up";

        var records = await context.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        var createdCount = records.Count(r => r.Action == SyncRecordAction.Created);
        var updatedCount = records.Count(r => r.Action == SyncRecordAction.Updated);
        var skippedCount = records.Count(r => r.Action == SyncRecordAction.Skipped);
        var downloadedCount = records.Count(r => r.Action == SyncRecordAction.Downloaded);
        var removedCount = records.Count(r => r.Action == SyncRecordAction.Removed);
        var errorCount = records.Count(r => r.Action == SyncRecordAction.Error);

        var removedFromSessionCount = 0;

        var validFilePaths = records
            .Where(r => r.Action is SyncRecordAction.Created or SyncRecordAction.Updated
                or SyncRecordAction.Skipped or SyncRecordAction.Downloaded)
            .Select(r => r.FilePath)
            .ToHashSet();

        // Handle orphaned song devices based on direction
        // Up: Remove ALL songs not in validFilePaths (regardless of SyncAction)
        // Both: Remove only songs with SyncAction == null not in validFilePaths
        // Down: Don't remove anything (server is not modified)
        List<SongDevice> orphanedSongDevices;
        
        if (direction == "both")
        {
            orphanedSongDevices = await context.SongDevices
                .Where(sd => sd.DeviceId == deviceId
                             && sd.SyncAction == null
                             && !validFilePaths.Contains(sd.DevicePath))
                .Include(sd => sd.Song)
                .ToListAsync(cancellationToken);
        }
        else if (direction == "up")
        {
            orphanedSongDevices = await context.SongDevices
                .Where(sd => sd.DeviceId == deviceId
                             && !validFilePaths.Contains(sd.DevicePath))
                .Include(sd => sd.Song)
                .ToListAsync(cancellationToken);
        }
        else // down
        {
            orphanedSongDevices = new List<SongDevice>();
        }

        removedFromSessionCount = orphanedSongDevices.Count;

        if (orphanedSongDevices.Count > 0)
        {
            var orphanedRecords = orphanedSongDevices.Select(sd => new DeviceSyncSessionRecord
            {
                SessionId = sessionId,
                SongId = sd.SongId,
                FilePath = sd.DevicePath,
                Action = SyncRecordAction.Removed,
                Source = SyncRecordSource.Server,
                Reason = direction == "up" ? "File missing from device (up direction)" : "File missing from device",
                ProcessedAt = DateTime.UtcNow,
            }).ToList();

            context.DeviceSyncSessionRecords.AddRange(orphanedRecords);

            if (!session.IsDryRun)
            {
                context.SongDevices.RemoveRange(orphanedSongDevices);
            }
        }

        // Handle pending actions for songs present on device
        // Up: Clear ALL pending actions for songs in validFilePaths
        // Both/Down: Don't clear anything (handled elsewhere)
        if (isUpDirection)
        {
            var songsToClear = await context.SongDevices
                .Where(sd => sd.DeviceId == deviceId
                             && validFilePaths.Contains(sd.DevicePath)
                             && sd.SyncAction != null)
                .ToListAsync(cancellationToken);

            if (songsToClear.Count > 0 && !session.IsDryRun)
            {
                foreach (var sd in songsToClear)
                {
                    sd.SyncAction = null;
                }
            }
        }

        session.CompletedAt = DateTime.UtcNow;
        session.Status = SyncSessionStatus.Completed;
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Completed sync session {SessionId}: Created={Created}, Updated={Updated}, Skipped={Skipped}, Downloaded={Downloaded}, Removed={Removed}, Error={Error}",
            sessionId, createdCount, updatedCount, skippedCount, downloadedCount,
            removedCount + removedFromSessionCount, errorCount);

        return new SyncCompleteResponse
        {
            CreatedCount = createdCount,
            UpdatedCount = updatedCount,
            SkippedCount = skippedCount,
            DownloadedCount = downloadedCount,
            RemovedCount = removedCount + removedFromSessionCount,
            ErrorCount = errorCount,
        };
    }

    [HttpGet("{deviceId:long}/sync/pending-actions")]
    public async Task<GetPendingActionsResponse> GetPendingActions(long deviceId, CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var pendingActions = await context.SongDevices
            .Include(sd => sd.Song)
            .Where(sd => sd.DeviceId == deviceId && sd.SyncAction != null && sd.SyncAction != SongSyncAction.Upload)
            .Select(sd => new PendingActionItem
            {
                SongId = sd.SongId,
                Path = sd.DevicePath,
                Action = sd.SyncAction!.Value.ToString(),
            })
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} pending actions for device {DeviceId}", pendingActions.Count, deviceId);

        return new GetPendingActionsResponse
        {
            Actions = pendingActions,
        };
    }

    [HttpGet("{deviceId:long}/sync/songs")]
    public async Task<GetDeviceSongsResponse> GetDeviceSongs(long deviceId, CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var songs = await context.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .Select(sd => new DeviceSongItem
            {
                SongId = sd.SongId,
                Path = sd.DevicePath,
                Action = sd.SyncAction != null ? sd.SyncAction.Value.ToString() : null,
            })
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} songs for device {DeviceId}", songs.Count, deviceId);

        return new GetDeviceSongsResponse
        {
            Songs = songs,
        };
    }

    [HttpPost("{deviceId:long}/sync/acknowledge")]
    public async Task<AcknowledgeActionResponse> AcknowledgeAction(long deviceId,
        [FromBody] AcknowledgeActionRequest request, CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var songDevice = await context.SongDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == request.SongId, cancellationToken);

        if (songDevice == null)
        {
            throw new Exception($"SongDevice not found for device {deviceId} and song {request.SongId}");
        }

        if (songDevice.SyncAction == SongSyncAction.Remove)
        {
            context.SongDevices.Remove(songDevice);
        }
        else
        {
            songDevice.SyncAction = null;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Acknowledged action for song {SongId} on device {DeviceId}", request.SongId, deviceId);

        return new AcknowledgeActionResponse { Success = true };
    }

    [HttpPost("{deviceId:long}/sync/check")]
    public async Task<SyncCheckResponse> CheckSync(long deviceId, [FromBody] SyncCheckRequest request,
        CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var existingSongDevices = await context.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        var toCreate = new List<SyncFileInfoItem>();
        var toUpdate = new List<SyncFileInfoItem>();

        foreach (var clientFile in request.Files)
        {
            var existingSongDevice = existingSongDevices.FirstOrDefault(sd => sd.DevicePath == clientFile.Path);

            if (existingSongDevice == null)
            {
                toCreate.Add(new SyncFileInfoItem
                {
                    Path = clientFile.Path,
                    ModifiedAt = clientFile.ModifiedAt,
                    CreatedAt = clientFile.CreatedAt,
                    Reason = $"No matching SongDevice found on server for path '{clientFile.Path}'",
                });
            }
            else if (request.Force)
            {
                toUpdate.Add(new SyncFileInfoItem
                {
                    Path = clientFile.Path,
                    ModifiedAt = clientFile.ModifiedAt,
                    CreatedAt = clientFile.CreatedAt,
                    Reason = "Force flag was set",
                });
            }
            else if (clientFile.ModifiedAt > existingSongDevice.LastSyncedModifiedAt)
            {
                toUpdate.Add(new SyncFileInfoItem
                {
                    Path = clientFile.Path,
                    ModifiedAt = clientFile.ModifiedAt,
                    CreatedAt = clientFile.CreatedAt,
                    Reason =
                        $"File modified at {clientFile.ModifiedAt:O} is newer than last synced modified at {existingSongDevice.LastSyncedModifiedAt:O}",
                });
            }
        }

        logger.LogInformation(
            "Sync check for device {DeviceId}: {ToCreate} to create, {ToUpdate} to update",
            deviceId, toCreate.Count, toUpdate.Count);

        return new SyncCheckResponse
        {
            ToCreate = toCreate,
            ToUpdate = toUpdate,
        };
    }

    [HttpPost("{deviceId:long}/sync/upload")]
    public async Task<SyncUploadResponse> UploadFile(
        long deviceId,
        IFormFile file,
        [FromForm] string path,
        [FromForm] string modifiedAt,
        [FromForm] string createdAt,
        CancellationToken cancellationToken)
    {
        var device = await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var repositoryPath = configuration["MyMusic:MusicRepositoryPath"]
                             ?? throw new Exception("MusicRepositoryPath not configured");

        var tempPath = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), $"mymusic_import_{Guid.NewGuid()}");
        fileSystem.Directory.CreateDirectory(tempPath);

        try
        {
            var tempFilePath = fileSystem.Path.Combine(tempPath, fileSystem.Path.GetFileName(path));
            await using (var stream = fileSystem.FileStream.New(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var modifiedAtDateTime = DateTime.Parse(modifiedAt, null, DateTimeStyles.RoundtripKind);
            var createdAtDateTime = DateTime.Parse(createdAt, null, DateTimeStyles.RoundtripKind);

            var songImportMetadata = new SongImportMetadata(tempFilePath, createdAtDateTime, modifiedAtDateTime);

            var job = new MusicImportJob(importJobLogger);

            await musicService.ImportRepositorySongs(
                context,
                job,
                currentUser.Id,
                new[] { songImportMetadata },
                new[] { deviceId },
                DuplicateSongsHandlingStrategy.Overwrite,
                cancellationToken);

            var importedSong = job.SongMapping.Values.FirstOrDefault();

            if (importedSong == null)
            {
                var skipReason = job.SkipReasons.FirstOrDefault(s => s.SourceFilePath == tempFilePath);
                var exception = job.Exceptions.FirstOrDefault();

                var errorParts = new List<string>();

                if (skipReason != null)
                {
                    errorParts.Add(FormatLogMessage(skipReason.Message, skipReason.MessageArgs));
                }

                if (exception != null)
                {
                    errorParts.Add($"Exception: {exception.Message}");
                }

                if (skipReason == null && exception == null)
                {
                    errorParts.Add("No song was imported and no skip reason was recorded");
                }

                throw new Exception(string.Join("; ", errorParts));
            }

            var existingSongDevice = await context.SongDevices
                .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.DevicePath == path, cancellationToken);

            if (existingSongDevice != null)
            {
                existingSongDevice.LastSyncedModifiedAt = DateTime.UtcNow;
            }
            else
            {
                var songDevice = new SongDevice
                {
                    DeviceId = deviceId,
                    SongId = importedSong.Id,
                    DevicePath = path,
                    AddedAt = createdAtDateTime,
                    LastSyncedModifiedAt = DateTime.UtcNow,
                };
                context.SongDevices.Add(songDevice);
            }

            device.LastSyncAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Uploaded file {Path} to device {DeviceId}, song ID: {SongId}",
                path, deviceId, importedSong.Id);

            return new SyncUploadResponse
            {
                Success = true,
                SongId = importedSong.Id,
            };
        }
        finally
        {
            if (fileSystem.Directory.Exists(tempPath))
            {
                fileSystem.Directory.Delete(tempPath, true);
            }
        }
    }

    private static string FormatLogMessage(string template, object[] args)
    {
        if (args == null || args.Length == 0)
        {
            return template;
        }

        var index = 0;
        var formattedTemplate = Regex.Replace(template, @"\{(\w+)\}", _ => $"{{{index++}}}");
        return string.Format(formattedTemplate, args);
    }

    [HttpGet("filter-metadata")]
    public FilterMetadataResponse GetFilterMetadata() =>
        new()
        {
            Fields =
            [
                new FilterFieldMetadata
                {
                    Name = "name",
                    Type = "string",
                    Description = "Device name",
                    SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
                    SupportsDynamicValues = true,
                },
                new FilterFieldMetadata
                {
                    Name = "icon",
                    Type = "string",
                    Description = "Device icon",
                    SupportedOperators = ["eq", "neq", "isNull", "isNotNull"],
                    SupportsDynamicValues = true,
                },
                new FilterFieldMetadata
                {
                    Name = "color",
                    Type = "string",
                    Description = "Device color",
                    SupportedOperators = ["eq", "neq", "isNull", "isNotNull"],
                    SupportsDynamicValues = true,
                },
                new FilterFieldMetadata
                {
                    Name = "lastSyncAt",
                    Type = "date",
                    Description = "Last sync date",
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "isNull", "isNotNull"],
                },
                new FilterFieldMetadata
                {
                    Name = "songCount",
                    Type = "number",
                    Description = "Number of songs",
                    IsComputed = true,
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
                },
                new FilterFieldMetadata
                {
                    Name = "searchableText",
                    Type = "string",
                    Description = "Combined searchable text",
                    IsComputed = true,
                    SupportedOperators = ["contains"],
                },
            ],
            Operators = FilterMetadataHelper.GetOperatorMetadata(),
        };

    [HttpGet("filter-values")]
    public async Task<FilterValuesResponse> GetFilterValues(
        [FromQuery] string field,
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = field switch
        {
            "name" => context.Devices
                .Where(d => d.OwnerId == currentUser.Id)
                .Select(d => d.Name)
                .Distinct(),
            "icon" => context.Devices
                .Where(d => d.OwnerId == currentUser.Id)
                .Select(d => d.Icon)
                .Where(v => v != null)
                .Cast<string>()
                .Distinct(),
            "color" => context.Devices
                .Where(d => d.OwnerId == currentUser.Id)
                .Select(d => d.Color)
                .Where(v => v != null)
                .Cast<string>()
                .Distinct(),
            _ => Enumerable.Empty<string>().AsQueryable(),
        };

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(v => v.ToLower().Contains(searchLower));
        }

        var values = await query
            .OrderBy(v => v)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new FilterValuesResponse { Values = values };
    }
}