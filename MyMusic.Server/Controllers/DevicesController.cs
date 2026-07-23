using System.Globalization;
using System.IO.Abstractions;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.Filters;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
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
    IConfiguration configuration,
    IOptions<Config> config,
    IFileSystem fileSystem,
    ISyncActionsServerFactory syncActionsServerFactory,
    ISyncCommitService syncCommitService,
    ISyncUploadService syncUploadService) : ControllerBase
{
    [HttpGet]
    public async Task<ListDevicesResponse> List(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null,
        [FromQuery] bool includeSongs = false)
    {
        var query = context.Devices
            .Where(d => d.OwnerId == currentUser.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = FuzzySearchHelper.ApplyFuzzySearch(query, search, d => d.SearchableText);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<Device>(filter);
            query = query.Where(filterExpression);
        }

        var devices = await query.ToListAsync(cancellationToken);
        var deviceIds = devices.Select(d => d.Id).ToList();

        var songDeviceGroups = await context.SongDevices
            .Where(sd => sd.SongId != null && deviceIds.Contains(sd.DeviceId))
            .GroupBy(sd => sd.DeviceId)
            .Select(g => new
            {
                DeviceId = g.Key,
                Count = g.Count(),
                SongRefs = includeSongs ? g.ToList() : null
            })
            .ToDictionaryAsync(x => x.DeviceId, x => x, cancellationToken);

        return new ListDevicesResponse
        {
            Devices = devices.Select(d =>
            {
                var group = songDeviceGroups.GetValueOrDefault(d.Id);
                var songs = includeSongs
                    ? (group?.SongRefs?.Select(sd => new DeviceSongRef { Id = sd.SongId!.Value, Path = sd.DevicePath, SyncAction = sd.SyncAction?.ToString() }).ToList() ?? [])
                    : null;
                return ListDeviceItem.FromEntity(d, group?.Count ?? 0, songs);
            }).ToList(),
        };
    }

    [HttpPost]
    public async Task<ActionResult<CreateDeviceResponse>> Create([FromBody] CreateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken);
        if (user == null) return NotFound("User not found");

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

        logger.LogInformation("Created device {DeviceName} with ID {DeviceId} for user {UserId}, Template={NamingTemplate}", device.Name, device.Id, currentUser.Id, device.NamingTemplate ?? "(null)");

        return new CreateDeviceResponse
        {
            Device = CreateDeviceItem.FromEntity(device),
        };
    }

    [HttpPut("{deviceId:long}")]
    public async Task<ActionResult<UpdateDeviceResponse>> Update(long deviceId, [FromBody] UpdateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        device.Icon = request.Icon;
        device.Color = request.Color;
        device.NamingTemplate = request.NamingTemplate;
        if (request.ImportOnPurchase.HasValue)
        {
            device.ImportOnPurchase = request.ImportOnPurchase.Value;
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
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        var sessionsForDevice = await context.DeviceSyncSessions
            .Where(s => s.DeviceId == deviceId)
            .Select(s => new { s.Id, s.RepositoryPath })
            .ToListAsync(cancellationToken);

        foreach (var session in sessionsForDevice)
        {
            StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);
        }

        await context.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.DeviceSyncSessionRecords
            .Where(r => context.DeviceSyncSessions
                .Where(s => s.DeviceId == deviceId)
                .Select(s => s.Id)
                .Contains(r.SessionId))
            .ExecuteDeleteAsync(cancellationToken);

        await context.DeviceSyncSessions
            .Where(s => s.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Devices
            .Where(d => d.Id == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Deleted device {DeviceId} for user {UserId}", deviceId, currentUser.Id);

        return NoContent();
    }

    [HttpGet("{deviceId:long}", Name = "GetDevice")]
    public async Task<ActionResult<GetDeviceResponse>> Get(long deviceId, CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        var songCount = await context.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .CountAsync(cancellationToken);

        return new GetDeviceResponse
        {
            Device = GetDeviceItem.FromEntity(device, songCount),
        };
    }

    [HttpGet("{deviceId:long}/sessions")]
    public async Task<ActionResult<ListSyncSessionsResponse>> ListSessions(
        long deviceId,
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

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
    public async Task<ActionResult<DeleteSessionResponse>> DeleteSession(
        long deviceId,
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, deviceId, cancellationToken);
        if (session == null) return NotFound();

        if (session.Status == SyncSessionStatus.InProgress &&
            session.StartedAt > DateTime.UtcNow.AddSeconds(-InProgressSafetyThresholdSeconds))
        {
            throw new Exception("Cannot delete a session that is currently in progress");
        }

        StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);

        var recordsDeleted = await context.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Deleted sync session {SessionId} and {RecordCount} records", sessionId, recordsDeleted);

        return new DeleteSessionResponse { Success = true };
    }

    [HttpPost("{deviceId:long}/sessions/prune")]
    public async Task<ActionResult<PruneSessionsResponse>> PruneSessions(
        long deviceId,
        [FromBody] PruneSessionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

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

        var sessionIds = sessionsToDelete.Select(s => s.Id).ToList();

        foreach (var session in sessionsToDelete)
        {
            StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);
        }

        var recordsDeleted = await context.DeviceSyncSessionRecords
            .Where(r => sessionIds.Contains(r.SessionId))
            .ExecuteDeleteAsync(cancellationToken);

        var sessionsDeleted = await context.DeviceSyncSessions
            .Where(s => sessionIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Pruned {DeletedCount} sync sessions and {RecordsDeleted} records for device {DeviceId}", sessionsDeleted, recordsDeleted, deviceId);

        return new PruneSessionsResponse { DeletedCount = sessionsDeleted };
    }

    [HttpGet("{deviceId:long}/sessions/{sessionId:long}/records")]
    public async Task<ActionResult<ListSyncRecordsResponse>> GetSessionRecords(
        long deviceId,
        long sessionId,
        [FromQuery] string? actions = null,
        [FromQuery] int? limit = null,
        [FromQuery] int? offset = null,
        [FromQuery] string? sort = null,
        [FromQuery] bool? includeSongInfo = null,
        [FromQuery] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, deviceId, cancellationToken);
        if (session == null) return NotFound();

        var query = context.DeviceSyncSessions
            .Where(s => s.Id == sessionId)
            .SelectMany(s => s.Records);

        // Conditionally include Song and Artists if requested
        if (includeSongInfo == true)
        {
            query = query
                .Include(r => r.Song)
                .ThenInclude(s => s.Artists)
                .ThenInclude(a => a.Artist);
        }

        // Apply filter DSL if provided
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<DeviceSyncSessionRecord>(filter, GetSessionRecordFieldMappings());
            query = query.Where(filterExpression);
        }

        // Keep existing actions filter for backward compatibility
        if (!string.IsNullOrEmpty(actions))
        {
            var actionList = actions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(a => Enum.Parse<SyncRecordAction>(a, true))
                .ToHashSet();
            query = query.Where(r => actionList.Contains(r.Action));
        }

        // Get total count for pagination metadata
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        IOrderedQueryable<DeviceSyncSessionRecord> orderedQuery;
        if (sort == "action_date")
        {
            orderedQuery = query
                .OrderBy(r => r.Action)
                .ThenBy(r => r.ProcessedAt)
                .ThenBy(r => r.Id);
        }
        else
        {
            orderedQuery = query.OrderBy(r => r.Id);
        }

        // Apply offset-based pagination
        if (offset.HasValue && offset.Value > 0)
        {
            orderedQuery = (IOrderedQueryable<DeviceSyncSessionRecord>)orderedQuery.Skip(offset.Value);
        }

        List<DeviceSyncSessionRecord> records;
        string? nextCursor = null;
        bool hasMore = false;

        if (limit.HasValue)
        {
            // Fetch one extra to determine if there are more records
            records = await orderedQuery
                .Take(limit.Value + 1)
                .ToListAsync(cancellationToken);

            // Check if we have more records
            if (records.Count > limit.Value)
            {
                hasMore = true;
                records.RemoveAt(records.Count - 1); // Remove the extra record
                var currentOffset = offset ?? 0;
                nextCursor = (currentOffset + limit.Value).ToString();
            }
        }
        else
        {
            // No limit specified - return all records (backward compatibility)
            records = await orderedQuery.ToListAsync(cancellationToken);
        }

        return new ListSyncRecordsResponse
        {
            Records = records.Select(r => SyncRecordResponseItem.FromEntity(r, includeSongInfo == true)).ToList(),
            NextCursor = nextCursor,
            HasMore = hasMore,
            TotalCount = totalCount,
        };
    }

    [HttpGet("{deviceId:long}/sessions/{sessionId:long}/records/filter-metadata")]
    public FilterMetadataResponse GetSessionRecordsFilterMetadata(
        long deviceId,
        long sessionId)
    {
        return new FilterMetadataResponse
        {
            Fields =
            [
                new FilterFieldMetadata
                {
                    Name = "filePath",
                    Type = "string",
                    Description = "File path of the synced file",
                    SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
                    SupportsDynamicValues = true,
                },
                new FilterFieldMetadata
                {
                    Name = "action",
                    Type = "enum",
                    Description = "Sync action performed",
                    SupportedOperators = ["eq", "neq", "in"],
                    Values = Enum.GetNames(typeof(SyncRecordAction)).ToList(),
                },
                new FilterFieldMetadata
                {
                    Name = "song",
                    EntityPath = "Song.SearchableText",
                    Type = "string",
                    Description = "Song title, album, or label",
                    IsComputed = true,
                    SupportedOperators = ["contains"],
                    SupportsDynamicValues = false,
                },
                new FilterFieldMetadata
                {
                    Name = "song.title",
                    EntityPath = "Song.Title",
                    Type = "string",
                    Description = "Song title",
                    SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
                    SupportsDynamicValues = true,
                },
                new FilterFieldMetadata
                {
                    Name = "song.artist.name",
                    EntityPath = "Song.Artists.Artist.Name",
                    Type = "string",
                    Description = "Song artist name",
                    IsCollection = true,
                    SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith"],
                    SupportsDynamicValues = true,
                },
                new FilterFieldMetadata
                {
                    Name = "song.album.name",
                    EntityPath = "Song.Album.Name",
                    Type = "string",
                    Description = "Song album name",
                    SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
                    SupportsDynamicValues = true,
                },
            ],
            Operators = FilterMetadataHelper.GetOperatorMetadata(),
        };
    }

    [HttpGet("{deviceId:long}/sessions/{sessionId:long}/records/filter-values")]
    public async Task<FilterValuesResponse> GetSessionRecordsFilterValues(
        long deviceId,
        long sessionId,
        [FromQuery] string field,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = field.ToLower() switch
        {
            "filepath" => context.DeviceSyncSessionRecords
                .Where(r => r.SessionId == sessionId && r.Session.DeviceId == deviceId && r.Session.Device.OwnerId == currentUser.Id)
                .Select(r => r.FilePath)
                .Distinct(),
            "song.title" => context.Songs
                .Where(s => s.OwnerId == currentUser.Id)
                .Select(s => s.Title)
                .Distinct(),
            "song.artist.name" => context.Artists
                .Where(a => a.OwnerId == currentUser.Id)
                .Select(a => a.Name)
                .Distinct(),
            "song.album.name" => context.Albums
                .Where(a => a.OwnerId == currentUser.Id)
                .Select(a => a.Name)
                .Distinct(),
            _ => Enumerable.Empty<string?>().AsQueryable(),
        };

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(v => v != null && v.ToLower().Contains(searchLower));
        }

        var values = await query
            .Where(v => v != null)
            .OrderBy(v => v)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new FilterValuesResponse { Values = values! };
    }

    [HttpPost("{deviceId:long}/sync/start")]
    public async Task<ActionResult<SyncStartResponse>> StartSync(long deviceId, [FromBody] SyncStartRequest? request,
        CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

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

        if (request?.ScanErrors is { Count: > 0 })
        {
            var syncActions = syncActionsServerFactory.Create(context, session.Id, deviceId, session.IsDryRun);
            foreach (var error in request.ScanErrors)
            {
                await syncActions.ActionError(error.FilePath, error.ErrorMessage, reason: $"Scan error: {error.ErrorMessage}", cancellationToken: cancellationToken);
            }
        }

        logger.LogInformation(
            "Started sync session {SessionId} for device {DeviceId} (DryRun: {IsDryRun}, RepositoryPath: {RepositoryPath})",
            session.Id, deviceId, session.IsDryRun, session.RepositoryPath);

        return new SyncStartResponse { SessionId = session.Id };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/complete")]
    public async Task<ActionResult<SyncCompleteResponse>> CompleteSync(long deviceId, long sessionId,
        [FromBody] SyncCompleteRequest? request, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(sessionId, deviceId, cancellationToken);
        if (session == null) return NotFound();

        if (session.Status == SyncSessionStatus.InProgress)
        {
            throw new Exception($"Sync session {sessionId} must be committed before completion (status: {session.Status})");
        }

        if (session.Status != SyncSessionStatus.Committed)
        {
            throw new Exception($"Sync session {sessionId} cannot be completed (status: {session.Status})");
        }

        session.CompletedAt = DateTime.UtcNow;
        session.Status = SyncSessionStatus.Completed;

        if (!session.IsDryRun)
        {
            var device = await context.Devices.FindAsync([deviceId], cancellationToken);
            if (device != null)
            {
                device.LastSyncAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var records = await context.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Completed sync session {SessionId}: Created={Created}, Updated={Updated}, Skipped={Skipped}, Downloaded={Downloaded}, Removed={Removed}, Error={Error}",
            sessionId,
            records.Count(r => r.Action == SyncRecordAction.CreateRemote),
            records.Count(r => r.Action == SyncRecordAction.UpdateRemote),
            records.Count(r => r.Action == SyncRecordAction.Skipped),
            records.Count(r => r.Action == SyncRecordAction.CreateLocal || r.Action == SyncRecordAction.UpdateLocal),
            records.Count(r => r.Action == SyncRecordAction.Delete || r.Action == SyncRecordAction.Unlink),
            records.Count(r => r.Action == SyncRecordAction.Error));

        return new SyncCompleteResponse
        {
            CreateRemoteCount = records.Count(r => r.Action == SyncRecordAction.CreateRemote),
            UpdateRemoteCount = records.Count(r => r.Action == SyncRecordAction.UpdateRemote),
            SkippedCount = records.Count(r => r.Action == SyncRecordAction.Skipped),
            CreateLocalCount = records.Count(r => r.Action == SyncRecordAction.CreateLocal),
            UpdateLocalCount = records.Count(r => r.Action == SyncRecordAction.UpdateLocal),
            DeleteCount = records.Count(r => r.Action == SyncRecordAction.Delete),
            LinkCount = records.Count(r => r.Action == SyncRecordAction.Link),
            UnlinkCount = records.Count(r => r.Action == SyncRecordAction.Unlink),
            RenameCount = records.Count(r => r.Action == SyncRecordAction.Rename),
            ConflictCount = records.Count(r => r.Action == SyncRecordAction.Conflict),
            UpdateTimestampCount = records.Count(r => r.Action == SyncRecordAction.UpdateTimestamp),
            ErrorCount = records.Count(r => r.Action == SyncRecordAction.Error),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/cancel")]
    public async Task<ActionResult<SyncCancelResponse>> CancelSync(long deviceId, long sessionId,
        CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(sessionId, deviceId, cancellationToken);
        if (session == null) return NotFound();

        if (session.Status != SyncSessionStatus.InProgress)
        {
            throw new Exception($"Sync session {sessionId} cannot be cancelled (status: {session.Status})");
        }

        session.Status = SyncSessionStatus.Cancelled;
        session.CompletedAt = DateTime.UtcNow;

        var stagingDeleted = StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cancelled sync session {SessionId} for device {DeviceId}", sessionId, deviceId);

        return new SyncCancelResponse
        {
            SessionId = sessionId,
            StagingDirectoryDeleted = stagingDeleted,
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/commit")]
    public async Task<ActionResult<SyncCommitResponse>> CommitSync(long deviceId, long sessionId,
        [FromBody] SyncCommitRequest? request, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(sessionId, deviceId, cancellationToken);
        if (session == null) return NotFound();

        if (session.Status != SyncSessionStatus.InProgress && session.Status != SyncSessionStatus.Committed)
        {
            throw new Exception($"Sync session {sessionId} cannot be committed (status: {session.Status})");
        }

        if (session.Status == SyncSessionStatus.Committed)
        {
            var existingRecords = await context.DeviceSyncSessionRecords
                .Where(r => r.SessionId == sessionId)
                .ToListAsync(cancellationToken);

            return MapCommitResponse(existingRecords, session.CompletedAt ?? DateTime.UtcNow);
        }

        var direction = request?.Direction?.ToLowerInvariant() ?? "both";

        var result = await syncCommitService.CommitAsync(context, sessionId, deviceId, session.IsDryRun, direction, cancellationToken);

        session.Status = SyncSessionStatus.Committed;
        session.CompletedAt = DateTime.UtcNow;

        StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Committed sync session {SessionId} for device {DeviceId}", sessionId, deviceId);

        return MapCommitResponse(result, session.CompletedAt.Value);
    }

    private static SyncCommitResponse MapCommitResponse(Dictionary<SyncRecordAction, int> counts, DateTime committedAt)
    {
        return new SyncCommitResponse
        {
            CreateRemoteCount = counts.GetValueOrDefault(SyncRecordAction.CreateRemote),
            UpdateRemoteCount = counts.GetValueOrDefault(SyncRecordAction.UpdateRemote),
            SkippedCount = counts.GetValueOrDefault(SyncRecordAction.Skipped),
            CreateLocalCount = counts.GetValueOrDefault(SyncRecordAction.CreateLocal),
            UpdateLocalCount = counts.GetValueOrDefault(SyncRecordAction.UpdateLocal),
            DeleteCount = counts.GetValueOrDefault(SyncRecordAction.Delete),
            LinkCount = counts.GetValueOrDefault(SyncRecordAction.Link),
            UnlinkCount = counts.GetValueOrDefault(SyncRecordAction.Unlink),
            RenameCount = counts.GetValueOrDefault(SyncRecordAction.Rename),
            ConflictCount = counts.GetValueOrDefault(SyncRecordAction.Conflict),
            UpdateTimestampCount = counts.GetValueOrDefault(SyncRecordAction.UpdateTimestamp),
            ErrorCount = counts.GetValueOrDefault(SyncRecordAction.Error),
            CommittedAt = committedAt,
        };
    }

    private static SyncCommitResponse MapCommitResponse(SyncCommitResult result, DateTime committedAt)
        => MapCommitResponse(result.ActionCounts, committedAt);

    private static SyncCommitResponse MapCommitResponse(List<DeviceSyncSessionRecord> records, DateTime committedAt)
        => MapCommitResponse(records.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.Count()), committedAt);

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/pending-actions")]
    public async Task<ActionResult<CreatePendingActionsResponse>> CreatePendingActions(long deviceId, long sessionId, CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        var records = await CreatePendingActionsForDevice(deviceId, device.NamingTemplate, sessionId, cancellationToken);

        logger.LogInformation("Created {Count} pending action records for device {DeviceId}", records.Count, deviceId);

        return new CreatePendingActionsResponse
        {
            Records = records.Select(r => SyncRecordResponseItem.FromEntity(r)).ToList(),
        };
    }

    [HttpGet("{deviceId:long}/sync/songs")]
    public async Task<ActionResult<GetDeviceSongsResponse>> GetDeviceSongs(long deviceId, CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

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

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/error")]
    public async Task<ActionResult<ReportSyncErrorResponse>> ReportSyncError(long deviceId, long sessionId,
        [FromBody] ReportSyncErrorRequest request, CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        var session = await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            return NotFound($"Sync session not found with id {sessionId}");
        }

        var syncActions = syncActionsServerFactory.Create(context, sessionId, deviceId, session.IsDryRun);
        var record = await syncActions.ActionError(request.FilePath, request.ErrorMessage, request.SongId, reason: request.ErrorMessage, cancellationToken);

        return new ReportSyncErrorResponse
        {
            Counts = SyncActionCounts.FromAction(SyncRecordAction.Error),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/acknowledge")]
    public async Task<ActionResult<AcknowledgeActionResponse>> AcknowledgeAction(long deviceId, long sessionId,
        [FromBody] AcknowledgeActionRequest request, CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        if (request.RecordIds is not { Count: > 0 })
        {
            return BadRequest("RecordIds is required");
        }

        var records = await context.DeviceSyncSessionRecords
            .Where(r => request.RecordIds.Contains(r.Id) && r.Session.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        var acknowledgedRecords = new List<DeviceSyncSessionRecord>();

        await syncCommitService.AcknowledgeRecordsAsync(records, request.ModifiedAt);

        acknowledgedRecords.AddRange(records);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Acknowledged {Count} records for device {DeviceId}", acknowledgedRecords.Count, deviceId);

        return new AcknowledgeActionResponse
        {
            Success = true,
            Counts = SyncActionCounts.FromRecords(acknowledgedRecords),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/check")]
    public async Task<ActionResult<SyncCheckResponse>> CheckSync(long deviceId, long sessionId, [FromBody] SyncCheckRequest request,
        CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        var activeSessionResult = await GetActiveSessionAsync(sessionId, deviceId, cancellationToken);
        if (activeSessionResult.Result != null) return activeSessionResult.Result;
        var activeSession = activeSessionResult.Value!;

        var syncActions = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, activeSession.IsDryRun);

        var namingStrategy = new TemplateNamingStrategy(
            device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

        // The naming strategy and the full set of device paths are only
        // needed by the CreateLocal fallback branch below, so load them once only when needed
        HashSet<string>? usedPaths = null;

        var clientPaths = request.Files.Select(f => f.Path).ToList();

        // Load only the SongDevices for paths the client reported in this chunk,
        // indexed by DevicePath for O(1) lookup inside the per-file loop.
        var existingSongDevicesByPath = await context.SongDevices
            .IncludeSongMetadata("Song")
            .Where(sd => sd.DeviceId == deviceId && clientPaths.Contains(sd.DevicePath))
            .ToDictionaryAsync(sd => sd.DevicePath, cancellationToken);

        var allRecords = new List<DeviceSyncSessionRecord>();

        foreach (var clientFile in request.Files)
        {
            var existingSongDevice = existingSongDevicesByPath.GetValueOrDefault(clientFile.Path);

            logger.LogDebug("CheckSync: Path='{Path}', DeviceId={DeviceId}, SongDeviceFound={Found}, SongId={SongId}, LastSyncedModifiedAt={LastSynced}",
                clientFile.Path, deviceId, existingSongDevice != null, existingSongDevice?.SongId, existingSongDevice?.LastSyncedModifiedAt);

            if (existingSongDevice == null)
            {
                logger.LogDebug("CheckSync: Path='{Path}' -> CREATE_REMOTE (no existing SongDevice)", clientFile.Path);
                var record = new DeviceSyncSessionRecord
                {
                    SessionId = activeSession.Id,
                    FilePath = clientFile.Path,
                    Action = SyncRecordAction.CreateRemote,
                    SongId = null,
                    Reason = $"No matching SongDevice found on server for path '{clientFile.Path}'",
                    Data = SyncActionDataSerializer.Serialize(new SyncCheckCreateUpdateData
                    {
                        ModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                        CreatedAt = clientFile.CreatedAt.ToUniversalTime(),
                        Reason = $"No matching SongDevice found on server for path '{clientFile.Path}'",
                    }),
                    ProcessedAt = DateTime.UtcNow,
                };
                allRecords.Add(record);
            }
            else if (existingSongDevice.Song == null)
            {
                logger.LogDebug("CheckSync: Path='{Path}' -> SKIPPED (Song was deleted, SongDevice kept for tracking removal)", clientFile.Path);
                var record = await syncActions.ActionSkipped(clientFile.Path, existingSongDevice.SongId, reason: "Song was deleted, SongDevice kept for tracking removal", cancellationToken: cancellationToken);
                allRecords.Add(record);
            }
            else if (existingSongDevice.SyncAction == SongSyncAction.Remove)
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UNLINK (marked for removal)", clientFile.Path, existingSongDevice.SongId);
                var record = await syncActions.ActionUnlink(existingSongDevice.DevicePath, existingSongDevice.SongId, "Song marked for removal", cancellationToken);
                allRecords.Add(record);
            }
            else if (request.Force)
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_REMOTE (Force flag)", clientFile.Path, existingSongDevice.SongId);
                allRecords.Add(new DeviceSyncSessionRecord
                {
                    SessionId = activeSession.Id,
                    FilePath = clientFile.Path,
                    Action = SyncRecordAction.UpdateRemote,
                    SongId = existingSongDevice.SongId,
                    Reason = "Force flag was set",
                    Data = SyncActionDataSerializer.Serialize(new SyncCheckCreateUpdateData
                    {
                        ModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                        CreatedAt = clientFile.CreatedAt.ToUniversalTime(),
                        Reason = "Force flag was set",
                    }),
                    ProcessedAt = DateTime.UtcNow,
                });
            }
            else if (existingSongDevice.LastSyncedModifiedAt == null)
            {
                if (existingSongDevice.SyncAction == SongSyncAction.Download)
                {
                    var referenceTime = existingSongDevice.AddedAt;
                    if (IsNewerThan(existingSongDevice.Song.ModifiedAt, referenceTime))
                    {
                        logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> CONFLICT (Download action, server modified since added)", clientFile.Path, existingSongDevice.SongId);
                        var record = await syncActions.ActionConflict(clientFile.Path, clientFile.ModifiedAt.ToUniversalTime(), existingSongDevice.Song.ModifiedAt.ToUniversalTime(), existingSongDevice.SongId, reason: "Conflict: server modified since device added and no sync timestamp", cancellationToken: cancellationToken);
                        record.Data = SyncActionDataSerializer.Serialize(new SyncCheckConflictData
                        {
                            LocalModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                            ServerModifiedAt = existingSongDevice.Song.ModifiedAt.ToUniversalTime(),
                            LastSyncedAt = existingSongDevice.LastSyncedModifiedAt?.ToUniversalTime(),
                            ServerChecksum = existingSongDevice.Song.Checksum,
                            ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                        });
                        context.Entry(record).Property(r => r.Data).IsModified = true;
                        await context.SaveChangesAsync(cancellationToken);
                        allRecords.Add(record);
                    }
                    else
                    {
                        logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_REMOTE (no sync timestamp, Download action, server not newer)", clientFile.Path, existingSongDevice.SongId);
                        allRecords.Add(new DeviceSyncSessionRecord
                        {
                            SessionId = activeSession.Id,
                            FilePath = clientFile.Path,
                            Action = SyncRecordAction.UpdateRemote,
                            SongId = existingSongDevice.SongId,
                            Reason = $"Local file exists but never synced, server has not changed since device was added (server modified at {existingSongDevice.Song.ModifiedAt:O})",
                            Data = SyncActionDataSerializer.Serialize(new SyncCheckCreateUpdateData
                            {
                                ModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                                CreatedAt = clientFile.CreatedAt.ToUniversalTime(),
                                Reason = $"Local file exists but never synced, server has not changed since device was added (server modified at {existingSongDevice.Song.ModifiedAt:O})",
                            }),
                            ProcessedAt = DateTime.UtcNow,
                        });
                    }
                }
                else
                {
                    logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_REMOTE (no sync timestamp)", clientFile.Path, existingSongDevice.SongId);
                    allRecords.Add(new DeviceSyncSessionRecord
                    {
                        SessionId = activeSession.Id,
                        FilePath = clientFile.Path,
                        Action = SyncRecordAction.UpdateRemote,
                        SongId = existingSongDevice.SongId,
                        Reason = $"Local file exists with no sync timestamp (local modified at {clientFile.ModifiedAt:O})",
                        Data = SyncActionDataSerializer.Serialize(new SyncCheckCreateUpdateData
                        {
                            ModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                            CreatedAt = clientFile.CreatedAt.ToUniversalTime(),
                            Reason = $"Local file exists with no sync timestamp (local modified at {clientFile.ModifiedAt:O})",
                        }),
                        ProcessedAt = DateTime.UtcNow,
                    });
                }
            }
            // Check if the device file was changed after the last sync.
            else if (IsNewerThan(clientFile.ModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
            {
                // Check if the server song was also changed after the last sync.
                if (IsNewerThan(existingSongDevice.Song.ModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
                {
                    logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> CONFLICT (local modified {LocalModifiedAt:O}, server modified {ServerModifiedAt:O}, last synced {LastSynced:O})",
                        clientFile.Path, existingSongDevice.SongId, clientFile.ModifiedAt, existingSongDevice.Song.ModifiedAt, existingSongDevice.LastSyncedModifiedAt);
                    allRecords.Add(new DeviceSyncSessionRecord
                    {
                        SessionId = activeSession.Id,
                        FilePath = clientFile.Path,
                        Action = SyncRecordAction.Conflict,
                        SongId = existingSongDevice.SongId,
                        Reason = "Conflict: both local and server modified since last sync",
                        Data = SyncActionDataSerializer.Serialize(new SyncCheckConflictData
                        {
                            LocalModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                            ServerModifiedAt = existingSongDevice.Song.ModifiedAt.ToUniversalTime(),
                            LastSyncedAt = existingSongDevice.LastSyncedModifiedAt?.ToUniversalTime(),
                            ServerChecksum = existingSongDevice.Song.Checksum,
                            ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                        }),
                        ProcessedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_REMOTE (file modified at {LocalModifiedAt:O} newer than last synced {LastSynced:O})",
                        clientFile.Path, existingSongDevice.SongId, clientFile.ModifiedAt, existingSongDevice.LastSyncedModifiedAt);
                    allRecords.Add(new DeviceSyncSessionRecord
                    {
                        SessionId = activeSession.Id,
                        FilePath = clientFile.Path,
                        Action = SyncRecordAction.UpdateRemote,
                        SongId = existingSongDevice.SongId,
                        Reason = $"File modified at {clientFile.ModifiedAt:O} is newer than last synced modified at {existingSongDevice.LastSyncedModifiedAt:O}",
                        Data = SyncActionDataSerializer.Serialize(new SyncCheckCreateUpdateData
                        {
                            ModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                            CreatedAt = clientFile.CreatedAt.ToUniversalTime(),
                            Reason = $"File modified at {clientFile.ModifiedAt:O} is newer than last synced modified at {existingSongDevice.LastSyncedModifiedAt:O}",
                        }),
                        ProcessedAt = DateTime.UtcNow,
                    });
                }
            }
            // Check if the server song was changed after the last sync (device file unchanged).
            else if (IsNewerThan(existingSongDevice.Song.ModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UPDATE_LOCAL (server modified {ServerModifiedAt:O}, last synced {LastSynced:O})",
                    clientFile.Path, existingSongDevice.SongId, existingSongDevice.Song.ModifiedAt, existingSongDevice.LastSyncedModifiedAt);

                if (existingSongDevice.SyncAction == SongSyncAction.Remove)
                {
                    var record = await syncActions.ActionUnlink(existingSongDevice.DevicePath, existingSongDevice.SongId, "Song marked for removal", cancellationToken);
                    allRecords.Add(record);
                }
                else if (existingSongDevice.LastSyncedModifiedAt != null)
                {
                    allRecords.Add(new DeviceSyncSessionRecord
                    {
                        SessionId = activeSession.Id,
                        FilePath = clientFile.Path,
                        Action = SyncRecordAction.UpdateLocal,
                        SongId = existingSongDevice.SongId!.Value,
                        Reason = $"Server modified since last sync (server modified at {existingSongDevice.Song.ModifiedAt:O}, last synced at {existingSongDevice.LastSyncedModifiedAt:O})",
                        Data = SyncActionDataSerializer.Serialize(new SyncCheckUpdateLocalData
                        {
                            LocalModifiedAt = clientFile.ModifiedAt.ToUniversalTime(),
                            ServerModifiedAt = existingSongDevice.Song.ModifiedAt.ToUniversalTime(),
                            LastSyncedAt = existingSongDevice.LastSyncedModifiedAt!.Value.ToUniversalTime(),
                            ServerChecksum = existingSongDevice.Song.Checksum,
                            ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                        }),
                        ProcessedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    // Lazy-load the naming strategy and the full set of device paths,
                    // which are only needed for naming-collision detection on CreateLocal.
                    usedPaths ??= await context.SongDevices
                        .Where(sd => sd.DeviceId == deviceId)
                        .Select(sd => sd.DevicePath)
                        .ToHashSetAsync(cancellationToken);

                    var pendingAction = ComputePendingActionPath(existingSongDevice, namingStrategy, usedPaths);
                    usedPaths.Add(pendingAction.Path);

                    var reason = $"Server modified at {existingSongDevice.Song.ModifiedAt:O} is newer than last synced at {existingSongDevice.LastSyncedModifiedAt:O}";
                    var record = await syncActions.ActionCreateLocal(pendingAction.Path, existingSongDevice.SongId, existingSongDevice.Song.ModifiedAt, reason, cancellationToken);
                    allRecords.Add(record);
                }
            }
            else
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> SKIPPED (unchanged)", clientFile.Path, existingSongDevice.SongId);
                var record = await syncActions.ActionSkipped(clientFile.Path, existingSongDevice.SongId, reason: "File unchanged since last sync", cancellationToken: cancellationToken);
                allRecords.Add(record);
            }
        }

        logger.LogInformation(
            "Sync check for device {DeviceId}: {TotalRecords} total records ({CreateRemote} create remote, {UpdateRemote} update remote, {Conflict} conflicts, {UpdateLocal} update local, {Skipped} skipped, {Link} link, {Unlink} unlink)",
            deviceId, allRecords.Count,
            allRecords.Count(r => r.Action == SyncRecordAction.CreateRemote),
            allRecords.Count(r => r.Action == SyncRecordAction.UpdateRemote),
            allRecords.Count(r => r.Action == SyncRecordAction.Conflict),
            allRecords.Count(r => r.Action == SyncRecordAction.UpdateLocal),
            allRecords.Count(r => r.Action == SyncRecordAction.Skipped),
            allRecords.Count(r => r.Action == SyncRecordAction.Link),
            allRecords.Count(r => r.Action == SyncRecordAction.Unlink));

        return new SyncCheckResponse
        {
            Records = allRecords.Select(r => SyncRecordResponseItem.FromEntity(r)).ToList(),
            Counts = SyncActionCounts.FromRecords(allRecords.Where(r => r.Action != SyncRecordAction.UpdateLocal && r.Action != SyncRecordAction.Conflict)),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/resolve-conflicts")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<SyncResolveConflictsResponse>> ResolveConflicts(
        long deviceId,
        long sessionId,
        [FromBody] SyncResolveConflictsRequest request,
        CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        var activeSessionResult = await GetActiveSessionAsync(sessionId, deviceId, cancellationToken);
        if (activeSessionResult.Result != null) return activeSessionResult.Result;
        var activeSession = activeSessionResult.Value!;

        var records = new List<SyncRecordResponseItem>();
        var resolveSyncRecords = new List<DeviceSyncSessionRecord>();

        var syncActions = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, activeSession.IsDryRun);

        foreach (var conflict in request.Conflicts)
        {
            var songDevice = await context.SongDevices
                .Include(sd => sd.Song)
                .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == conflict.SongId, cancellationToken);

            if (songDevice == null)
            {
                logger.LogWarning("SongDevice not found for device {DeviceId} and song {SongId}", deviceId, conflict.SongId);
                continue;
            }

            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(conflict.FileContentBase64);
            }
            catch (FormatException ex)
            {
                logger.LogError(ex, "Invalid base64 content for {Path}", conflict.Path);

                var errorRecord = await syncActions.ActionError(conflict.Path, "Invalid file content format", conflict.SongId, "Invalid file content format", cancellationToken);
                resolveSyncRecords.Add(errorRecord);
                records.Add(SyncRecordResponseItem.FromEntity(errorRecord));

                continue;
            }

            string localChecksum = ChecksumService.ComputeChecksumFromBytes(fileBytes, songDevice.Song.ChecksumAlgorithm);

            if (localChecksum == songDevice.Song.Checksum)
            {
                var localModifiedAtUtc = conflict.LocalModifiedAt.ToUniversalTime();

                var newLastSynced = localModifiedAtUtc > songDevice.Song.ModifiedAt
                    ? localModifiedAtUtc
                    : songDevice.Song.ModifiedAt;

                var tsRecord = await syncActions.ActionUpdateTimestamp(conflict.Path, newLastSynced, conflict.SongId, "Timestamp update: checksums match, no file change needed", modifiedAt: conflict.LocalModifiedAt, createdAt: songDevice.AddedAt, cancellationToken: cancellationToken);
                resolveSyncRecords.Add(tsRecord);
                records.Add(SyncRecordResponseItem.FromEntity(tsRecord));

                logger.LogInformation(
                    "Resolved conflict for {Path} - checksums match, updated LastSyncedModifiedAt to {LastSyncedAt}",
                    conflict.Path, newLastSynced);
            }
            else
            {
                var conflictRecord = await syncActions.ActionConflict(conflict.Path, conflict.LocalModifiedAt, songDevice.Song.ModifiedAt, conflict.SongId, "Conflict: local and server both modified, checksums differ", localChecksum: localChecksum, serverChecksum: songDevice.Song.Checksum, algorithm: songDevice.Song.ChecksumAlgorithm, cancellationToken);
                resolveSyncRecords.Add(conflictRecord);
                records.Add(SyncRecordResponseItem.FromEntity(conflictRecord));

                logger.LogError(
                    "Conflict detected for {Path} - checksums differ (local: {LocalChecksum}, server: {ServerChecksum}), marking as error",
                    conflict.Path, localChecksum, songDevice.Song.Checksum);
            }
        }

        // Process potential updates: server was modified after last sync, client file was unchanged.
        // Compare checksums to determine if a local update is actually needed.
        if (request.PotentialUpdates.Count > 0)
        {
            var namingStrategy = new TemplateNamingStrategy(
                device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

            var usedPaths = new HashSet<string>(await context.SongDevices
                .Where(sd => sd.DeviceId == deviceId)
                .Select(sd => sd.DevicePath)
                .ToHashSetAsync(cancellationToken));

            foreach (var update in request.PotentialUpdates)
            {
                var songDevice = await context.SongDevices
                    .IncludeSongMetadata("Song")
                    .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == update.SongId, cancellationToken);

                if (songDevice == null)
                {
                    logger.LogWarning("SongDevice not found for device {DeviceId} and song {SongId} during potential update resolution", deviceId, update.SongId);
                    continue;
                }

                if (songDevice.Song == null)
                {
                    logger.LogWarning("Song not found for SongDevice device {DeviceId} and song {SongId} during potential update resolution", deviceId, update.SongId);
                    continue;
                }

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(update.FileContentBase64);
                }
                catch (FormatException ex)
                {
                    logger.LogError(ex, "Invalid base64 content for potential update {Path}", update.Path);

                    var errorRecord = await syncActions.ActionError(update.Path, "Invalid file content format", update.SongId, "Invalid file content format", cancellationToken);
                    resolveSyncRecords.Add(errorRecord);
                    records.Add(SyncRecordResponseItem.FromEntity(errorRecord));

                    continue;
                }

                var localChecksum = ChecksumService.ComputeChecksumFromBytes(fileBytes, songDevice.Song.ChecksumAlgorithm);

                if (localChecksum == songDevice.Song.Checksum)
                {
                    var newLastSynced = update.LocalModifiedAt.ToUniversalTime() > songDevice.Song.ModifiedAt
                        ? update.LocalModifiedAt.ToUniversalTime()
                        : songDevice.Song.ModifiedAt;

                    var tsRecord = await syncActions.ActionUpdateTimestamp(update.Path, newLastSynced, update.SongId, "Timestamp update: server was modified but checksums match, no local update needed", modifiedAt: update.LocalModifiedAt, createdAt: songDevice.AddedAt, cancellationToken: cancellationToken);
                    resolveSyncRecords.Add(tsRecord);
                    records.Add(SyncRecordResponseItem.FromEntity(tsRecord));

                    logger.LogInformation(
                        "Resolved potential update for {Path} (SongId={SongId}) - checksums match, updated LastSyncedModifiedAt to {LastSyncedAt}",
                        update.Path, update.SongId, newLastSynced);
                }
                else
                {
                    var pendingAction = ComputePendingActionPath(songDevice, namingStrategy, usedPaths);
                    usedPaths.Add(pendingAction.Path);

                    var updateFilePath = pendingAction.PreviousPath ?? pendingAction.Path;
                    var reason = $"Server modified at {songDevice.Song.ModifiedAt:O} is newer than last synced at {update.LastSyncedAt:O}, checksums differ";
                    var updateRecord = await syncActions.ActionUpdateLocal(updateFilePath, update.SongId, songDevice.Song.ModifiedAt, reason, cancellationToken);
                    resolveSyncRecords.Add(updateRecord);
                    records.Add(SyncRecordResponseItem.FromEntity(updateRecord));

                    if (pendingAction.PreviousPath != null)
                    {
                        var renameRecord = await syncActions.ActionRename(pendingAction.Path, pendingAction.PreviousPath, pendingAction.Path, update.SongId, "Path updated by naming template", cancellationToken);
                        resolveSyncRecords.Add(renameRecord);
                        records.Add(SyncRecordResponseItem.FromEntity(renameRecord));
                    }

                    logger.LogInformation(
                        "Potential update for {Path} (SongId={SongId}) - checksums differ, creating UpdateLocal action",
                        update.Path, update.SongId);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resolved conflicts for device {DeviceId}: {RecordCount} records",
            deviceId, records.Count);

        return new SyncResolveConflictsResponse
        {
            Records = records,
            Counts = SyncActionCounts.FromRecords(resolveSyncRecords),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<SyncUploadResponse>> UploadFile(
        long deviceId,
        long sessionId,
        IFormFile file,
        [FromForm] string path,
        [FromForm] string modifiedAt,
        [FromForm] string createdAt,
        CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        var activeSessionResult = await GetActiveSessionAsync(sessionId, deviceId, cancellationToken);
        if (activeSessionResult.Result != null) return activeSessionResult.Result;
        var activeSession = activeSessionResult.Value!;

        var repositoryPath = configuration["MyMusic:MusicRepositoryPath"]
                             ?? throw new Exception("MusicRepositoryPath not configured");

        var modifiedAtDateTime = DateTime.Parse(modifiedAt, null, DateTimeStyles.RoundtripKind).ToUniversalTime();
        var createdAtDateTime = DateTime.Parse(createdAt, null, DateTimeStyles.RoundtripKind).ToUniversalTime();

        var songDeviceForImport = await context.SongDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.DevicePath == path, cancellationToken);

        var result = await syncUploadService.UploadAsync(
            deviceId, activeSession.Id, activeSession.IsDryRun, path, file.OpenReadStream(),
            fileSystem.Path.GetFileName(path),
            modifiedAtDateTime, createdAtDateTime,
            isUpdate: songDeviceForImport != null,
            songDeviceForImport: songDeviceForImport,
            repositoryPath: repositoryPath,
            ownerId: currentUser.Id,
            cancellationToken: cancellationToken);

        return new SyncUploadResponse
        {
            Success = true,
            SongId = result.EffectiveSongId,
            RecordId = result.Record.Id,
            Action = result.Record.Action.ToString(),
            Data = result.Record.Data,
            Counts = SyncActionCounts.FromAction(result.Record.Action),
        };
    }

    private async Task<List<DeviceSyncSessionRecord>> CreatePendingActionsForDevice(long deviceId, string? namingTemplate, long sessionId, CancellationToken cancellationToken)
    {
        logger.LogInformation("CreatePendingActionsForDevice: DeviceId={DeviceId}, Template={NamingTemplate}, Default={DefaultNamingTemplate}", deviceId, namingTemplate ?? "(null)", config.Value.DefaultNamingTemplate);

        var existingRecordSongIds = await context.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId && r.SongId != null)
            .Select(r => r.SongId!.Value)
            .ToHashSetAsync(cancellationToken);

        var songDevices = await context.SongDevices
            .IncludeSongMetadata("Song")
            .Where(sd => sd.DeviceId == deviceId
                && sd.SyncAction != null
                && sd.SyncAction != SongSyncAction.Upload
                && !existingRecordSongIds.Contains(sd.SongId!.Value))
            .ToListAsync(cancellationToken);

        var allExistingPaths = await context.SongDevices
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
                var record = DeviceSyncSessionRecordForAction(sessionId, SyncRecordAction.Unlink, sd.DevicePath, sd.SongId, sd.SyncActionReason);
                createdRecords.Add(record);
            }
            else if (sd.SyncAction == SongSyncAction.Download)
            {
                var (path, previousPath) = ComputePendingActionPath(sd, namingStrategy, usedPaths);
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
            context.DeviceSyncSessionRecords.AddRange(createdRecords);
            await context.SaveChangesAsync(cancellationToken);
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

    private static (string Path, string? PreviousPath) ComputePendingActionPath(
        SongDevice sd, TemplateNamingStrategy namingStrategy, HashSet<string> usedPaths)
    {
        if (sd.Song != null)
        {
            var metadata = EntityConverter.ToSong(sd.Song);
            var naming = NamingMetadata.FromPath(sd.DevicePath);
            var basePath = namingStrategy.Generate(metadata, naming);
            var newPath = basePath == sd.DevicePath
                ? basePath
                : GetUniquePath(basePath, usedPaths);

            return newPath != sd.DevicePath
                ? (newPath, sd.DevicePath)
                : (sd.DevicePath, null);
        }

        return (sd.DevicePath, null);
    }

    private static string GetUniquePath(string basePath, HashSet<string> existingPaths)
    {
        if (!existingPaths.Contains(basePath))
        {
            return basePath;
        }

        var directory = Path.GetDirectoryName(basePath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        var counter = 2;
        string newPath;
        do
        {
            newPath = Path.Combine(directory, $"{fileNameWithoutExt} ({counter}){extension}");
            counter++;
        } while (existingPaths.Contains(newPath));

        return newPath;
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

    private static bool IsNewerThan(DateTime current, DateTime reference)
    {
        // The dates that are saved in the database by EF Core seem to lose the precision for the last digit
        // it is always 0 (zero) when reading it back, so we make the comparison without it in both values
        return current.Ticks / 10 > reference.Ticks / 10;
    }

    private static Dictionary<string, string> GetSessionRecordFieldMappings()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["song"] = "Song.SearchableText",
            ["song.title"] = "Song.Title",
            ["song.artist.name"] = "Song.Artists.Artist.Name",
            ["song.album.name"] = "Song.Album.Name",
        };
    }

    private async Task<Device?> FindDeviceAsync(long deviceId, CancellationToken cancellationToken)
    {
        return await context.Devices
            .Where(d => d.Id == deviceId && d.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<DeviceSyncSession?> FindSessionAsync(long sessionId, long deviceId, CancellationToken cancellationToken)
    {
        return await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ActionResult<DeviceSyncSession>> GetActiveSessionAsync(long sessionId, long deviceId, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(sessionId, deviceId, cancellationToken);
        if (session == null) return NotFound();
        if (session.Status != SyncSessionStatus.InProgress)
            throw new Exception($"Sync session {sessionId} is not in progress (status: {session.Status})");
        return session;
    }
}
