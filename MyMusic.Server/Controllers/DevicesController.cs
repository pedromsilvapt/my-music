using System.Globalization;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Models;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Devices;
using MyMusic.Server.DTO.Sync;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController(
    ILogger<DevicesController> logger,
    ICurrentUser currentUser,
    MusicDbContext context,
    IMusicService musicService,
    IConfiguration configuration,
    ILogger<MusicImportJob> importJobLogger,
    IFileSystem fileSystem) : ControllerBase
{
    private readonly IConfiguration _configuration = configuration;
    private readonly MusicDbContext _context = context;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger<MusicImportJob> _importJobLogger = importJobLogger;
    private readonly ILogger<DevicesController> _logger = logger;
    private readonly IMusicService _musicService = musicService;

    [HttpGet]
    public async Task<ListDevicesResponse> List(CancellationToken cancellationToken)
    {
        var devices = await _context.Devices
            .Where(d => d.OwnerId == _currentUser.Id)
            .ToListAsync(cancellationToken);

        var songCounts = await _context.SongDevices
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
        var user = await _context.Users.FindAsync([_currentUser.Id], cancellationToken)
                   ?? throw new Exception("User not found");

        var device = new Device
        {
            Name = request.Name,
            Icon = request.Icon,
            Color = request.Color,
            NamingTemplate = request.NamingTemplate,
            OwnerId = _currentUser.Id,
            Owner = user,
        };

        _context.Devices.Add(device);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created device {DeviceName} with ID {DeviceId} for user {UserId}",
            device.Name, device.Id, _currentUser.Id);

        return new CreateDeviceResponse
        {
            Device = CreateDeviceItem.FromEntity(device),
        };
    }

    [HttpPut("{deviceId:long}")]
    public async Task<UpdateDeviceResponse> Update(long deviceId, [FromBody] UpdateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var device = await _context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        device.Icon = request.Icon;
        device.Color = request.Color;
        device.NamingTemplate = request.NamingTemplate;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated device {DeviceId} for user {UserId}", deviceId, _currentUser.Id);

        return new UpdateDeviceResponse
        {
            Device = new UpdateDeviceItem
            {
                Id = device.Id,
                Name = device.Name,
                Icon = device.Icon,
                Color = device.Color,
                NamingTemplate = device.NamingTemplate,
            },
        };
    }

    [HttpDelete("{deviceId:long}")]
    public async Task<IActionResult> Delete(long deviceId, CancellationToken cancellationToken)
    {
        var device = await _context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var songDevices = await _context.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .ToListAsync(cancellationToken);
        _context.SongDevices.RemoveRange(songDevices);

        var sessions = await _context.DeviceSyncSessions
            .Where(s => s.DeviceId == deviceId)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            var records = await _context.DeviceSyncSessionRecords
                .Where(r => r.SessionId == session.Id)
                .ToListAsync(cancellationToken);
            _context.DeviceSyncSessionRecords.RemoveRange(records);
        }

        _context.DeviceSyncSessions.RemoveRange(sessions);

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted device {DeviceId} for user {UserId}", deviceId, _currentUser.Id);

        return NoContent();
    }

    [HttpGet("{deviceId:long}/sessions")]
    public async Task<ListSyncSessionsResponse> ListSessions(
        long deviceId,
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        var device = await _context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var sessions = await _context.DeviceSyncSessions
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

    [HttpGet("{deviceId:long}/sessions/{sessionId:long}/records")]
    public async Task<ListSyncRecordsResponse> GetSessionRecords(
        long deviceId,
        long sessionId,
        [FromQuery] string? actions = null,
        [FromQuery] string? source = null,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new Exception($"Sync session not found with id {sessionId}");
        }

        var query = _context.DeviceSyncSessions
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
        var device = await _context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == _currentUser.Id)
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
        };

        _context.DeviceSyncSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Started sync session {SessionId} for device {DeviceId} (DryRun: {IsDryRun})",
            session.Id, deviceId, session.IsDryRun);

        return new SyncStartResponse { SessionId = session.Id };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/records")]
    public async Task<SyncRecordsResponse> RecordChunk(long deviceId, long sessionId,
        [FromBody] SyncRecordsRequest request, CancellationToken cancellationToken)
    {
        var session = await _context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new Exception($"Sync session not found with id {sessionId}");
        }

        if (session.Status != SyncSessionStatus.InProgress)
        {
            throw new Exception($"Sync session {sessionId} is not in progress (status: {session.Status})");
        }

        var records = request.Records.Select(r => new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = r.FilePath,
            Action = Enum.Parse<SyncRecordAction>(r.Action),
            SongId = r.SongId,
            ErrorMessage = r.ErrorMessage,
            Reason = r.Reason,
            Source = string.IsNullOrEmpty(r.Source) ? SyncRecordSource.Device : Enum.Parse<SyncRecordSource>(r.Source),
            ProcessedAt = DateTime.UtcNow,
        }).ToList();

        _context.DeviceSyncSessionRecords.AddRange(records);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recorded {Count} records for session {SessionId}", records.Count, sessionId);

        return new SyncRecordsResponse { Success = true };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/complete")]
    public async Task<SyncCompleteResponse> CompleteSync(long deviceId, long sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new Exception($"Sync session not found with id {sessionId}");
        }

        if (session.Status != SyncSessionStatus.InProgress)
        {
            throw new Exception($"Sync session {sessionId} is not in progress (status: {session.Status})");
        }

        var records = await _context.DeviceSyncSessionRecords
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

        var orphanedSongDevices = await _context.SongDevices
            .Where(sd => sd.DeviceId == deviceId
                         && sd.SyncAction == null
                         && !validFilePaths.Contains(sd.DevicePath))
            .ToListAsync(cancellationToken);

        removedFromSessionCount = orphanedSongDevices.Count;

        if (!session.IsDryRun)
        {
            _context.SongDevices.RemoveRange(orphanedSongDevices);
        }

        session.CompletedAt = DateTime.UtcNow;
        session.Status = SyncSessionStatus.Completed;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
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
        var device = await _context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var pendingActions = await _context.SongDevices
            .Include(sd => sd.Song)
            .Where(sd => sd.DeviceId == deviceId && sd.SyncAction != null && sd.SyncAction != SongSyncAction.Upload)
            .Select(sd => new PendingActionItem
            {
                SongId = sd.SongId,
                Path = sd.DevicePath,
                Action = sd.SyncAction!.Value.ToString(),
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} pending actions for device {DeviceId}", pendingActions.Count, deviceId);

        return new GetPendingActionsResponse
        {
            Actions = pendingActions,
        };
    }

    [HttpPost("{deviceId:long}/sync/acknowledge")]
    public async Task<AcknowledgeActionResponse> AcknowledgeAction(long deviceId,
        [FromBody] AcknowledgeActionRequest request, CancellationToken cancellationToken)
    {
        var device = await _context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var songDevice = await _context.SongDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == request.SongId, cancellationToken);

        if (songDevice == null)
        {
            throw new Exception($"SongDevice not found for device {deviceId} and song {request.SongId}");
        }

        songDevice.SyncAction = null;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Acknowledged action for song {SongId} on device {DeviceId}", request.SongId, deviceId);

        return new AcknowledgeActionResponse { Success = true };
    }

    [HttpPost("{deviceId:long}/sync/check")]
    public async Task<SyncCheckResponse> CheckSync(long deviceId, [FromBody] SyncCheckRequest request,
        CancellationToken cancellationToken)
    {
        var device = await _context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var existingSongDevices = await _context.SongDevices
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

        _logger.LogInformation(
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
        var device = await _context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == _currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var repositoryPath = _configuration["MyMusic:MusicRepositoryPath"]
                             ?? throw new Exception("MusicRepositoryPath not configured");

        var tempPath = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), $"mymusic_import_{Guid.NewGuid()}");
        _fileSystem.Directory.CreateDirectory(tempPath);

        try
        {
            var tempFilePath = _fileSystem.Path.Combine(tempPath, _fileSystem.Path.GetFileName(path));
            await using (var stream = _fileSystem.FileStream.New(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var modifiedAtDateTime = DateTime.Parse(modifiedAt, null, DateTimeStyles.RoundtripKind);
            var createdAtDateTime = DateTime.Parse(createdAt, null, DateTimeStyles.RoundtripKind);

            var songImportMetadata = new SongImportMetadata(tempFilePath, createdAtDateTime, modifiedAtDateTime);

            var job = new MusicImportJob(_importJobLogger);

            await _musicService.ImportRepositorySongs(
                _context,
                job,
                _currentUser.Id,
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

            var existingSongDevice = await _context.SongDevices
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
                _context.SongDevices.Add(songDevice);
            }

            device.LastSyncAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Uploaded file {Path} to device {DeviceId}, song ID: {SongId}",
                path, deviceId, importedSong.Id);

            return new SyncUploadResponse
            {
                Success = true,
                SongId = importedSong.Id,
            };
        }
        finally
        {
            if (_fileSystem.Directory.Exists(tempPath))
            {
                _fileSystem.Directory.Delete(tempPath, true);
            }
        }
    }

    private static string FormatLogMessage(string template, object[] args)
    {
        if (args == null || args.Length == 0)
            return template;

        var index = 0;
        var formattedTemplate = Regex.Replace(template, @"\{(\w+)\}", _ => $"{{{index++}}}");
        return string.Format(formattedTemplate, args);
    }
}