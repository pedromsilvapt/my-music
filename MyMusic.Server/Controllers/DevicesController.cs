using System.Globalization;
using System.IO.Abstractions;
using System.IO.Hashing;
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
    IMusicService musicService,
    IConfiguration configuration,
    IOptions<Config> config,
    IFileSystem fileSystem,
    ISyncActionsServerFactory syncActionsServerFactory,
    ISyncCommitService syncCommitService) : ControllerBase
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
            var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<Device>(filter);
            query = query.Where(filterExpression);
        }

        var devices = await query.ToListAsync(cancellationToken);

        var songDeviceGroups = await context.SongDevices
            .Where(sd => devices.Select(d => d.Id).Contains(sd.DeviceId))
            .GroupBy(sd => sd.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count(), SongRefs = g.Select(sd => new { sd.SongId, sd.DevicePath, sd.SyncAction }).ToList() })
            .ToDictionaryAsync(x => x.DeviceId, x => x, cancellationToken);

        return new ListDevicesResponse
        {
            Devices = devices.Select(d =>
            {
                var group = songDeviceGroups.GetValueOrDefault(d.Id);
                var songs = group?.SongRefs.Select(sr => new DeviceSongRef { Id = sr.SongId!.Value, Path = sr.DevicePath, SyncAction = sr.SyncAction?.ToString() }).ToList() ?? [];
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

        var activeSession = await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Status == SyncSessionStatus.InProgress)
            .FirstOrDefaultAsync(cancellationToken);

        var namingStrategy = new TemplateNamingStrategy(
            device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

        var allExistingPaths = await context.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .Select(sd => sd.DevicePath)
            .ToHashSetAsync(cancellationToken);

        var usedPaths = new HashSet<string>(allExistingPaths);

        var existingSongDevices = await context.SongDevices
            .IncludeSongMetadata("Song")
            .Where(sd => sd.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        var toCreate = new List<SyncFileInfoItem>();
        var toUpdate = new List<SyncFileInfoItem>();
        var potentialConflicts = new List<SyncPotentialConflictItem>();
        var potentialUpdates = new List<SyncPotentialUpdateItem>();
        var createdSessionRecords = new List<DeviceSyncSessionRecord>();
        var skippedFiles = new List<SyncFileInfoItem>();

        foreach (var clientFile in request.Files)
        {
            var existingSongDevice = existingSongDevices.FirstOrDefault(sd => sd.DevicePath == clientFile.Path);

            logger.LogDebug("CheckSync: Path='{Path}', DeviceId={DeviceId}, SongDeviceFound={Found}, SongId={SongId}, LastSyncedModifiedAt={LastSynced}",
                clientFile.Path, deviceId, existingSongDevice != null, existingSongDevice?.SongId, existingSongDevice?.LastSyncedModifiedAt);

            if (existingSongDevice == null)
            {
                logger.LogDebug("CheckSync: Path='{Path}' -> TO CREATE (no existing SongDevice)", clientFile.Path);
                toCreate.Add(clientFile with
                {
                    Reason = $"No matching SongDevice found on server for path '{clientFile.Path}'"
                });
            }
            else if (existingSongDevice.Song == null)
            {
                logger.LogDebug("CheckSync: Path='{Path}' -> SKIP (Song was deleted, SongDevice kept for tracking removal)", clientFile.Path);
                skippedFiles.Add(clientFile with { Reason = "Song was deleted, SongDevice kept for tracking removal" });
            }
            else if (existingSongDevice.SyncAction == SongSyncAction.Remove)
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> UNLINK (marked for removal)", clientFile.Path, existingSongDevice.SongId);
                if (activeSession != null)
                {
                    var syncActions = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, activeSession.IsDryRun);
                    var pendingAction = ComputePendingActionPath(existingSongDevice, namingStrategy, usedPaths);
                    usedPaths.Add(pendingAction.Path);
                    var record = await syncActions.ActionUnlink(pendingAction.Path, existingSongDevice.SongId, "Song marked for removal", cancellationToken);
                    createdSessionRecords.Add(record);
                }
            }
            else if (request.Force)
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> TO UPDATE (Force flag)", clientFile.Path, existingSongDevice.SongId);
                toUpdate.Add(clientFile with { Reason = "Force flag was set" });
            }
            else if (existingSongDevice.LastSyncedModifiedAt == null)
            {
                if (existingSongDevice.SyncAction == SongSyncAction.Download)
                {
                    var referenceTime = existingSongDevice.AddedAt;
                    if (IsNewerThan(existingSongDevice.Song.ModifiedAt, referenceTime))
                    {
                        potentialConflicts.Add(new SyncPotentialConflictItem
                        {
                            Path = clientFile.Path,
                            LocalModifiedAt = clientFile.ModifiedAt,
                            ServerModifiedAt = existingSongDevice.Song.ModifiedAt,
                            LastSyncedAt = existingSongDevice.LastSyncedModifiedAt,
                            SongId = existingSongDevice.SongId,
                            ServerChecksum = existingSongDevice.Song.Checksum,
                            ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                        });
                    }
                    else
                    {
                        toUpdate.Add(clientFile with { Reason = $"Local file exists but never synced, server has not changed since device was added (server modified at {existingSongDevice.Song.ModifiedAt:O})" });
                    }
                }
                else
                {
                    logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> TO UPDATE (no sync timestamp)", clientFile.Path, existingSongDevice.SongId);
                    toUpdate.Add(clientFile with { Reason = $"Local file exists with no sync timestamp (local modified at {clientFile.ModifiedAt:O})" });
                }
            }
            // Check if the device file was changed after the last sync.
            else if (IsNewerThan(clientFile.ModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
            {
                // Check if the server song was changed after the last sync.
                if (IsNewerThan(existingSongDevice.Song.ModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
                {
                    logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> POTENTIAL CONFLICT (local modified {LocalModifiedAt:O}, server modified {ServerModifiedAt:O}, last synced {LastSynced:O})",
                        clientFile.Path, existingSongDevice.SongId, clientFile.ModifiedAt, existingSongDevice.Song.ModifiedAt, existingSongDevice.LastSyncedModifiedAt);
                    potentialConflicts.Add(new SyncPotentialConflictItem
                    {
                        Path = clientFile.Path,
                        LocalModifiedAt = clientFile.ModifiedAt,
                        ServerModifiedAt = existingSongDevice.Song.ModifiedAt,
                        LastSyncedAt = existingSongDevice.LastSyncedModifiedAt,
                        SongId = existingSongDevice.SongId,
                        ServerChecksum = existingSongDevice.Song.Checksum,
                        ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                    });
                }
                else
                {
                    logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> TO UPDATE (file modified at {LocalModifiedAt:O} newer than last synced {LastSynced:O})",
                        clientFile.Path, existingSongDevice.SongId, clientFile.ModifiedAt, existingSongDevice.LastSyncedModifiedAt);
                    toUpdate.Add(clientFile with { Reason = $"File modified at {clientFile.ModifiedAt:O} is newer than last synced modified at {existingSongDevice.LastSyncedModifiedAt:O}" });
                }
            }
            // Check if the server song was changed after the last sync (device file unchanged).
            else if (IsNewerThan(existingSongDevice.Song.ModifiedAt, existingSongDevice.LastSyncedModifiedAt!.Value))
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> POTENTIAL UPDATE (server modified {ServerModifiedAt:O}, last synced {LastSynced:O})",
                    clientFile.Path, existingSongDevice.SongId, existingSongDevice.Song.ModifiedAt, existingSongDevice.LastSyncedModifiedAt);

                if (existingSongDevice.SyncAction == SongSyncAction.Remove)
                {
                    var pendingAction = ComputePendingActionPath(existingSongDevice, namingStrategy, usedPaths);
                    usedPaths.Add(pendingAction.Path);
                    if (activeSession != null)
                    {
                        var syncActions = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, activeSession.IsDryRun);
                        var record = await syncActions.ActionUnlink(pendingAction.Path, existingSongDevice.SongId, "Song marked for removal", cancellationToken);
                        createdSessionRecords.Add(record);
                    }
                }
                else if (existingSongDevice.LastSyncedModifiedAt != null)
                {
                    potentialUpdates.Add(new SyncPotentialUpdateItem
                    {
                        Path = clientFile.Path,
                        LocalModifiedAt = clientFile.ModifiedAt,
                        ServerModifiedAt = existingSongDevice.Song.ModifiedAt,
                        LastSyncedAt = existingSongDevice.LastSyncedModifiedAt!.Value,
                        SongId = existingSongDevice.SongId!.Value,
                        ServerChecksum = existingSongDevice.Song.Checksum,
                        ServerChecksumAlgorithm = existingSongDevice.Song.ChecksumAlgorithm,
                    });
                }
                else
                {
                    var pendingAction = ComputePendingActionPath(existingSongDevice, namingStrategy, usedPaths);
                    usedPaths.Add(pendingAction.Path);
                    if (activeSession != null)
                    {
                        var syncActions = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, activeSession.IsDryRun);
                        if (pendingAction.PreviousPath != null)
                        {
                            var renameRecord = await syncActions.ActionRename(pendingAction.Path, pendingAction.PreviousPath, pendingAction.Path, existingSongDevice.SongId, "Path updated by naming template", cancellationToken);
                            createdSessionRecords.Add(renameRecord);
                        }

                        var reason = $"Server modified at {existingSongDevice.Song.ModifiedAt:O} is newer than last synced at {existingSongDevice.LastSyncedModifiedAt:O}";
                        var record = await syncActions.ActionCreateLocal(pendingAction.Path, existingSongDevice.SongId, existingSongDevice.Song.ModifiedAt, reason, cancellationToken);
                        createdSessionRecords.Add(record);
                    }
                }
            }
            else
            {
                logger.LogDebug("CheckSync: Path='{Path}' SongId={SongId} -> SKIPPED (unchanged)", clientFile.Path, existingSongDevice.SongId);
                skippedFiles.Add(clientFile with { Reason = "File unchanged since last sync" });
            }
        }

        var skippedRecordIds = new List<long>();
        var checkSyncRecords = new List<DeviceSyncSessionRecord>();

        if (skippedFiles.Count > 0 && activeSession != null)
        {
            var syncActions = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, activeSession.IsDryRun);
            foreach (var clientFile in skippedFiles)
            {
                var existingSd = existingSongDevices.FirstOrDefault(sd => sd.DevicePath == clientFile.Path);
                var record = await syncActions.ActionSkipped(clientFile.Path, existingSd?.SongId, reason: clientFile.Reason ?? "No changes detected", cancellationToken: cancellationToken);
                skippedRecordIds.Add(record.Id);
                checkSyncRecords.Add(record);
            }
        }

        logger.LogInformation(
            "Sync check for device {DeviceId}: {ToCreate} to create, {ToUpdate} to update, {PotentialConflicts} potential conflicts, {PotentialUpdates} potential updates, {NewRecords} new session records, {Skipped} skipped",
            deviceId, toCreate.Count, toUpdate.Count, potentialConflicts.Count, potentialUpdates.Count, createdSessionRecords.Count, skippedRecordIds.Count);

        return new SyncCheckResponse
        {
            ToCreate = toCreate,
            ToUpdate = toUpdate,
            PotentialConflicts = potentialConflicts,
            PotentialUpdates = potentialUpdates,
            Records = createdSessionRecords.Select(r => SyncRecordResponseItem.FromEntity(r)).ToList(),
            SkippedRecordIds = skippedRecordIds,
            Counts = SyncActionCounts.FromRecords(checkSyncRecords),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/resolve-conflicts")]
    public async Task<ActionResult<SyncResolveConflictsResponse>> ResolveConflicts(
        long deviceId,
        long sessionId,
        [FromBody] SyncResolveConflictsRequest request,
        CancellationToken cancellationToken)
    {
        var device = await FindDeviceAsync(deviceId, cancellationToken);
        if (device == null) return NotFound();

        var activeSession = await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Status == SyncSessionStatus.InProgress)
            .FirstOrDefaultAsync(cancellationToken);

        var toUpload = new List<SyncFileInfoItem>();
        var resolved = new List<SyncFileInfoItem>();
        var conflicts = new List<SyncConflictErrorItem>();
        var conflictRecords = new List<SyncActionRecordResponseItem>();
        var updateTimestampRecords = new List<SyncActionRecordResponseItem>();
        var updateLocalRecords = new List<SyncActionRecordResponseItem>();
        var renameRecords = new List<SyncActionRecordResponseItem>();
        var resolveSyncRecords = new List<DeviceSyncSessionRecord>();

        var syncActions = activeSession != null
            ? syncActionsServerFactory.Create(context, activeSession.Id, deviceId, activeSession.IsDryRun)
            : null;

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

                if (syncActions != null)
                {
                    var errorRecord = await syncActions.ActionError(conflict.Path, "Invalid file content format", conflict.SongId, "Invalid file content format", cancellationToken);
                    resolveSyncRecords.Add(errorRecord);
                    conflictRecords.Add(SyncActionRecordResponseItem.FromEntity(errorRecord));
                }

                conflicts.Add(new SyncConflictErrorItem
                {
                    Path = conflict.Path,
                    Reason = "Invalid file content format",
                });
                continue;
            }

            string localChecksum = ComputeChecksum(fileBytes, songDevice.Song.ChecksumAlgorithm);

            if (localChecksum == songDevice.Song.Checksum)
            {
                var localModifiedAtUtc = conflict.LocalModifiedAt.ToUniversalTime();

                var newLastSynced = localModifiedAtUtc > songDevice.Song.ModifiedAt
                    ? localModifiedAtUtc
                    : songDevice.Song.ModifiedAt;

                if (syncActions != null)
                {
                    var tsRecord = await syncActions.ActionUpdateTimestamp(conflict.Path, newLastSynced, conflict.SongId, "Timestamp update: checksums match, no file change needed", cancellationToken);
                    resolveSyncRecords.Add(tsRecord);
                    updateTimestampRecords.Add(SyncActionRecordResponseItem.FromEntity(tsRecord));
                }

                resolved.Add(new SyncFileInfoItem
                {
                    Path = conflict.Path,
                    ModifiedAt = conflict.LocalModifiedAt,
                    CreatedAt = songDevice.AddedAt,
                    Reason = "Checksums match - resolved by updating LastSyncedModifiedAt",
                });

                logger.LogInformation(
                    "Resolved conflict for {Path} - checksums match, updated LastSyncedModifiedAt to {LastSyncedAt}",
                    conflict.Path, newLastSynced);
            }
            else
            {
                if (syncActions != null)
                {
                    var conflictRecord = await syncActions.ActionConflict(conflict.Path, conflict.LocalModifiedAt, songDevice.Song.ModifiedAt, conflict.SongId, "Conflict: local and server both modified, checksums differ", cancellationToken);
                    resolveSyncRecords.Add(conflictRecord);
                    conflictRecords.Add(SyncActionRecordResponseItem.FromEntity(conflictRecord));
                }

                conflicts.Add(new SyncConflictErrorItem
                {
                    Path = conflict.Path,
                    Reason = $"Checksum mismatch - local: {localChecksum}, server: {songDevice.Song.Checksum}",
                });

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

                    if (syncActions != null)
                    {
                        var errorRecord = await syncActions.ActionError(update.Path, "Invalid file content format", update.SongId, "Invalid file content format", cancellationToken);
                        resolveSyncRecords.Add(errorRecord);
                        updateLocalRecords.Add(SyncActionRecordResponseItem.FromEntity(errorRecord));
                    }

                    conflicts.Add(new SyncConflictErrorItem
                    {
                        Path = update.Path,
                        Reason = "Invalid file content format",
                    });
                    continue;
                }

                var localChecksum = ComputeChecksum(fileBytes, songDevice.Song.ChecksumAlgorithm);

                if (localChecksum == songDevice.Song.Checksum)
                {
                    // Checksums match: no need to update the local file, just update the sync timestamp
                    var newLastSynced = update.LocalModifiedAt.ToUniversalTime() > songDevice.Song.ModifiedAt
                        ? update.LocalModifiedAt.ToUniversalTime()
                        : songDevice.Song.ModifiedAt;

                    if (syncActions != null)
                    {
                        var tsRecord = await syncActions.ActionUpdateTimestamp(update.Path, newLastSynced, update.SongId, "Timestamp update: server was modified but checksums match, no local update needed", cancellationToken);
                        resolveSyncRecords.Add(tsRecord);
                        updateTimestampRecords.Add(SyncActionRecordResponseItem.FromEntity(tsRecord));
                    }

                    resolved.Add(new SyncFileInfoItem
                    {
                        Path = update.Path,
                        ModifiedAt = update.LocalModifiedAt,
                        CreatedAt = songDevice.AddedAt,
                        Reason = "Checksums match - server was modified but local file is current",
                    });

                    logger.LogInformation(
                        "Resolved potential update for {Path} (SongId={SongId}) - checksums match, updated LastSyncedModifiedAt to {LastSyncedAt}",
                        update.Path, update.SongId, newLastSynced);
                }
                else
                {
                    // Checksums differ: the local file needs to be updated from the server
                    var pendingAction = ComputePendingActionPath(songDevice, namingStrategy, usedPaths);
                    usedPaths.Add(pendingAction.Path);

                    if (syncActions != null)
                    {
                        if (pendingAction.PreviousPath != null)
                        {
                            var renameRecord = await syncActions.ActionRename(pendingAction.Path, pendingAction.PreviousPath, pendingAction.Path, update.SongId, "Path updated by naming template", cancellationToken);
                            resolveSyncRecords.Add(renameRecord);
                            renameRecords.Add(SyncActionRecordResponseItem.FromEntity(renameRecord));
                        }

                        var updateFilePath = pendingAction.PreviousPath ?? pendingAction.Path;
                        var reason = $"Server modified at {songDevice.Song.ModifiedAt:O} is newer than last synced at {update.LastSyncedAt:O}, checksums differ";
                        var updateRecord = await syncActions.ActionUpdateLocal(updateFilePath, update.SongId, songDevice.Song.ModifiedAt, reason, cancellationToken);
                        resolveSyncRecords.Add(updateRecord);
                        updateLocalRecords.Add(SyncActionRecordResponseItem.FromEntity(updateRecord));
                    }

                    logger.LogInformation(
                        "Potential update for {Path} (SongId={SongId}) - checksums differ, creating UpdateLocal action",
                        update.Path, update.SongId);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resolved conflicts for device {DeviceId}: {Resolved} resolved, {ToUpload} to upload, {Conflicts} conflicts, {UpdateLocal} update local actions, {Rename} rename actions",
            deviceId, resolved.Count, toUpload.Count, conflicts.Count, updateLocalRecords.Count, renameRecords.Count);

        return new SyncResolveConflictsResponse
        {
            ToUpload = toUpload,
            Resolved = resolved,
            Conflicts = conflicts,
            ConflictRecords = conflictRecords,
            UpdateTimestampRecords = updateTimestampRecords,
            UpdateLocalRecords = updateLocalRecords,
            RenameRecords = renameRecords,
            Counts = SyncActionCounts.FromRecords(resolveSyncRecords),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/upload")]
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

        var activeSession = await context.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Status == SyncSessionStatus.InProgress)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSession == null)
        {
            return NotFound($"No active sync session found for device {deviceId} with session {sessionId}");
        }

        var repositoryPath = configuration["MyMusic:MusicRepositoryPath"]
                             ?? throw new Exception("MusicRepositoryPath not configured");

        var modifiedAtDateTime = DateTime.Parse(modifiedAt, null, DateTimeStyles.RoundtripKind).ToUniversalTime();
        var createdAtDateTime = DateTime.Parse(createdAt, null, DateTimeStyles.RoundtripKind).ToUniversalTime();

        var songDeviceForImport = await context.SongDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.DevicePath == path, cancellationToken);

        return await UploadFileStaged(device, deviceId, file, path, repositoryPath, activeSession,
            songDeviceForImport, modifiedAtDateTime, createdAtDateTime, cancellationToken);
    }

    private async Task<SyncUploadResponse> UploadFileStaged(
        Device device, long deviceId, IFormFile file, string path, string repositoryPath,
        DeviceSyncSession activeSession, SongDevice? songDeviceForImport,
        DateTime modifiedAtDateTime, DateTime createdAtDateTime, CancellationToken cancellationToken)
    {
        var isUpdate = songDeviceForImport != null;
        var isDryRun = activeSession.IsDryRun;

        var tempPath = fileSystem.Path.Combine(repositoryPath, ".temp", $"sync-{activeSession.Id}");
        string? stagingFilePath = null;

        if (!isDryRun)
        {
            fileSystem.Directory.CreateDirectory(tempPath);
            var stagingFileName = $"{Guid.NewGuid()}-{fileSystem.Path.GetFileName(path)}";
            stagingFilePath = fileSystem.Path.Combine(tempPath, stagingFileName);
            await using (var stream = fileSystem.FileStream.New(stagingFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }
        }
        else
        {
            var systemTempPath = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), $"mymusic_staging_dryrun_{Guid.NewGuid()}");
            fileSystem.Directory.CreateDirectory(systemTempPath);
            try
            {
                var tempFilePath = fileSystem.Path.Combine(systemTempPath, fileSystem.Path.GetFileName(path));
                await using (var stream = fileSystem.FileStream.New(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                var checksumAlgorithm = ChecksumService.CreateChecksumAlgorithm();
                var checksumAlgorithmName = checksumAlgorithm.GetType().Name;
                var checksum = ChecksumService.CalculateChecksum(fileSystem, checksumAlgorithm, tempFilePath);

                long? songId = isUpdate ? songDeviceForImport!.SongId!.Value : null;

                var (duplicateSongId, hasDuplicate) = await FindDuplicateForUploadAsync(deviceId, activeSession.Id, checksum, checksumAlgorithmName, cancellationToken);

                var syncActions = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, isDryRun);
                DeviceSyncSessionRecord record;
                if (isUpdate)
                {
                    record = await syncActions.ActionUpdateRemote(path, songId, checksum, checksumAlgorithmName,
                        modifiedAtDateTime, tempFilePath: null, createdAtDateTime, originalFilePath: null, reason: "File re-uploaded (updated)", cancellationToken);
                }
                else if (hasDuplicate && duplicateSongId.HasValue)
                {
                    record = await syncActions.ActionLink(path, duplicateSongId.Value, modifiedAtDateTime, checksum, checksumAlgorithmName, "Linked to existing song (duplicate checksum)", cancellationToken);
                }
                else if (hasDuplicate)
                {
                    record = await syncActions.ActionLink(path, checksum, checksumAlgorithmName, modifiedAtDateTime, "Linked to existing song (duplicate checksum)", cancellationToken);
                }
                else
                {
                    record = await syncActions.ActionCreateRemote(path, songId, checksum, checksumAlgorithmName,
                        modifiedAtDateTime, tempFilePath: null, createdAtDateTime, originalFilePath: null, reason: "New file uploaded", cancellationToken);
                }

                await context.SaveChangesAsync(cancellationToken);

                return new SyncUploadResponse
                {
                    Success = true,
                    SongId = duplicateSongId ?? songId,
                    RecordId = record.Id,
                    Action = record.Action.ToString(),
                    Data = record.Data,
                    Counts = SyncActionCounts.FromAction(record.Action),
                };
            }
            finally
            {
                if (fileSystem.Directory.Exists(systemTempPath))
                {
                    fileSystem.Directory.Delete(systemTempPath, true);
                }
            }
        }

        var checksumAlg = ChecksumService.CreateChecksumAlgorithm();
        var checksumAlgName = checksumAlg.GetType().Name;
        var fileChecksum = ChecksumService.CalculateChecksum(fileSystem, checksumAlg, stagingFilePath!);

        long? songIdForRecord = isUpdate ? songDeviceForImport!.SongId!.Value : null;

        var (stagedDuplicateSongId, stagedHasDuplicate) = await FindDuplicateForUploadAsync(deviceId, activeSession.Id, fileChecksum, checksumAlgName, cancellationToken);

        var originalFilePath = fileSystem.Path.Combine(tempPath, fileSystem.Path.GetFileName(path));

        if (isUpdate)
        {
            // When updating, don't downgrade to Link even if a duplicate song exists.
            // The force-sync mechanism explicitly requests re-upload of unchanged files,
            // and the update should be recorded as UpdateRemote, not Link.
            var syncActionsServerForUpdate = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, isDryRun);
            var updateRecord = await syncActionsServerForUpdate.ActionUpdateRemote(path, songIdForRecord, fileChecksum, checksumAlgName,
                modifiedAtDateTime, stagingFilePath, createdAtDateTime, originalFilePath, "File re-uploaded (updated)", cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Uploaded file {Path} to device {DeviceId} (forced update), song ID: {SongId}",
                path, deviceId, songIdForRecord);

            return new SyncUploadResponse
            {
                Success = true,
                SongId = songIdForRecord,
                RecordId = updateRecord.Id,
                Action = updateRecord.Action.ToString(),
                Data = updateRecord.Data,
                Counts = SyncActionCounts.FromAction(updateRecord.Action),
            };
        }

        if (stagedHasDuplicate)
        {
            fileSystem.File.Delete(stagingFilePath!);

            var syncActionsServer = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, isDryRun);
            DeviceSyncSessionRecord linkRecord;

            if (stagedDuplicateSongId.HasValue)
            {
                linkRecord = await syncActionsServer.ActionLink(path, stagedDuplicateSongId.Value, modifiedAtDateTime, fileChecksum, checksumAlgName, "Linked to existing song (duplicate checksum)", cancellationToken);
                logger.LogInformation("Uploaded file {Path} to device {DeviceId} linked to existing song {SongId}",
                    path, deviceId, stagedDuplicateSongId.Value);
            }
            else
            {
                linkRecord = await syncActionsServer.ActionLink(path, fileChecksum, checksumAlgName, modifiedAtDateTime, "Linked to existing song (duplicate checksum)", cancellationToken);
                logger.LogInformation("Uploaded file {Path} to device {DeviceId} linked via checksum to pending song",
                    path, deviceId);
            }

            await context.SaveChangesAsync(cancellationToken);

            return new SyncUploadResponse
            {
                Success = true,
                SongId = stagedDuplicateSongId,
                RecordId = linkRecord.Id,
                Action = linkRecord.Action.ToString(),
                Data = linkRecord.Data,
                Counts = SyncActionCounts.FromAction(linkRecord.Action),
            };
        }

        var syncActionsServerForCreate = syncActionsServerFactory.Create(context, activeSession.Id, deviceId, isDryRun);
        DeviceSyncSessionRecord stagedRecord;
        stagedRecord = await syncActionsServerForCreate.ActionCreateRemote(path, songIdForRecord, fileChecksum, checksumAlgName,
            modifiedAtDateTime, stagingFilePath, createdAtDateTime, originalFilePath, "New file uploaded", cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Uploaded file {Path} to device {DeviceId} (staged), song ID: {SongId}",
            path, deviceId, songIdForRecord);

        return new SyncUploadResponse
        {
            Success = true,
            SongId = songIdForRecord,
            RecordId = stagedRecord.Id,
            Action = stagedRecord.Action.ToString(),
            Data = stagedRecord.Data,
            Counts = SyncActionCounts.FromAction(stagedRecord.Action),
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
            var (path, previousPath) = ComputePendingActionPath(sd, namingStrategy, usedPaths);
            usedPaths.Add(path);

            if (sd.SyncAction == SongSyncAction.Remove)
            {
                var record = DeviceSyncSessionRecordForAction(sessionId, SyncRecordAction.Unlink, path, sd.SongId, sd.SyncActionReason);
                createdRecords.Add(record);
            }
            else if (sd.SyncAction == SongSyncAction.Download)
            {
                var isUpdate = sd.LastSyncedModifiedAt != null;
                var action = isUpdate ? SyncRecordAction.UpdateLocal : SyncRecordAction.CreateLocal;

                if (previousPath != null)
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

    private async Task<(long? SongId, bool HasDuplicateChecksum)> FindDuplicateForUploadAsync(
        long deviceId, long sessionId, string checksum, string checksumAlgorithm,
        CancellationToken cancellationToken)
    {
        var sessionRecords = await context.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId
                        && (r.Action == SyncRecordAction.CreateRemote || r.Action == SyncRecordAction.Link))
            .ToListAsync(cancellationToken);

        foreach (var r in sessionRecords)
        {
            if (r.Data == null) continue;
            var recordData = r.Action == SyncRecordAction.CreateRemote
                ? (SyncActionDataSerializer.Deserialize<CreateRemoteData>(r.Data) as object ??
                   (SyncActionDataSerializer.Deserialize<SongModifiedAtData>(r.Data) as object))
                : SyncActionDataSerializer.Deserialize<SongModifiedAtData>(r.Data);

            var recordChecksum = recordData switch
            {
                CreateRemoteData crd => crd.Checksum,
                SongModifiedAtData smd => smd.Checksum,
                _ => null
            };
            if (recordChecksum != checksum) continue;

            var recordSongId = recordData switch
            {
                CreateRemoteData crd => crd.SongId,
                SongModifiedAtData smd => smd.SongId,
                _ => null
            };

            if (recordSongId.HasValue && recordSongId.Value > 0)
                return (recordSongId.Value, true);
        }

        var hasDuplicateInSession = sessionRecords.Any(r =>
        {
            if (r.Data == null) return false;
            var data = r.Action == SyncRecordAction.CreateRemote
                ? (SyncActionDataSerializer.Deserialize<CreateRemoteData>(r.Data) as object ??
                   (SyncActionDataSerializer.Deserialize<SongModifiedAtData>(r.Data) as object))
                : SyncActionDataSerializer.Deserialize<SongModifiedAtData>(r.Data);
            var dataChecksum = data switch
            {
                CreateRemoteData crd => crd.Checksum,
                SongModifiedAtData smd => smd.Checksum,
                _ => null
            };
            return dataChecksum == checksum;
        });

        if (hasDuplicateInSession)
            return (null, true);

        var existingSongs = await musicService.FindUserSongsByChecksum(
            context, currentUser.Id, [checksum], checksumAlgorithm, cancellationToken);

        if (existingSongs.TryGetValue(checksum, out var existingSong))
        {
            return (existingSong.Id, true);
        }

        return (null, false);
    }

    private static bool IsNewerThan(DateTime current, DateTime reference)
    {
        // The dates that are saved in the database by EF Core seem to lose the precision for the last digit
        // it is always 0 (zero) when reading it back, so we make the comparison without it in both values
        return current.Ticks / 10 > reference.Ticks / 10;
    }

    private static string ComputeChecksum(byte[] fileBytes, string checksumAlgorithm)
    {
        if (checksumAlgorithm == "MD5")
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            return Convert.ToBase64String(md5.ComputeHash(fileBytes));
        }

        var xxHash = new XxHash128();
        xxHash.Append(fileBytes);
        return Convert.ToBase64String(xxHash.GetCurrentHash());
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
}
