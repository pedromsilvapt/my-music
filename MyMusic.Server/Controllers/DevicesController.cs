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
using MyMusic.Common.Services.Devices;
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
    ISyncUploadService syncUploadService,
    IDeviceLookupService deviceLookup,
    ISyncSessionLookupService sessionLookup,
    ISyncPathResolver pathResolver,
    ISyncComparisonHelper comparisonHelper,
    IDeviceListService deviceListService,
    IDeviceGetService deviceGetService,
    IDeviceCreateService deviceCreateService,
    IDeviceUpdateService deviceUpdateService,
    IDeviceDeleteService deviceDeleteService,
    IDeviceFilterValuesService deviceFilterValuesService) : ControllerBase
{
    [HttpGet]
    public async Task<ListDevicesResponse> List(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null,
        [FromQuery] bool includeSongs = false)
    {
        var result = await deviceListService.ListAsync(currentUser.Id, search, filter, includeSongs, cancellationToken);

        return new ListDevicesResponse
        {
            Devices = result.Devices.Select(entry =>
            {
                var songs = entry.SongRefs?
                    .Select(sr => new DeviceSongRef { Id = sr.SongId, Path = sr.DevicePath, SyncAction = sr.SyncAction?.ToString() })
                    .ToList();
                return ListDeviceItem.FromEntity(entry.Device, entry.SongCount, songs);
            }).ToList(),
        };
    }

    [HttpPost]
    public async Task<ActionResult<CreateDeviceResponse>> Create([FromBody] CreateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await deviceCreateService.CreateAsync(
            new DeviceCreateInput
            {
                Name = request.Name,
                Icon = request.Icon,
                Color = request.Color,
                NamingTemplate = request.NamingTemplate,
                ImportOnPurchase = request.ImportOnPurchase,
            },
            cancellationToken);
        if (result == null) return NotFound("User not found");

        return new CreateDeviceResponse
        {
            Device = CreateDeviceItem.FromEntity(result.Device),
        };
    }

    [HttpPut("{deviceId:long}")]
    public async Task<ActionResult<UpdateDeviceResponse>> Update(long deviceId, [FromBody] UpdateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await deviceUpdateService.UpdateAsync(
            deviceId,
            new DeviceUpdateInput
            {
                Icon = request.Icon,
                Color = request.Color,
                NamingTemplate = request.NamingTemplate,
                ImportOnPurchase = request.ImportOnPurchase,
            },
            cancellationToken);
        if (result == null) return NotFound();

        var device = result.Device;
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
        var deleted = await deviceDeleteService.DeleteAsync(deviceId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{deviceId:long}", Name = "GetDevice")]
    public async Task<ActionResult<GetDeviceResponse>> Get(long deviceId, CancellationToken cancellationToken)
    {
        var result = await deviceGetService.GetAsync(deviceId, currentUser.Id, cancellationToken);
        if (result == null) return NotFound();

        return new GetDeviceResponse
        {
            Device = GetDeviceItem.FromEntity(result.Device, result.SongCount),
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

                // Use FileModifiedAt (the file-content change time) rather than ModifiedAt (which
                // also bumps on metadata-only edits). This prevents a metadata-only edit from
                // inflating the device's new LastSyncedModifiedAt, which would mask real future
                // file-content changes.
                var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;

                var newLastSynced = localModifiedAtUtc > songFileModifiedAt
                    ? localModifiedAtUtc
                    : songFileModifiedAt;

                var tsRecord = await syncActions.ActionUpdateTimestamp(conflict.Path, newLastSynced, conflict.SongId, "Timestamp update: checksums match, no file change needed", modifiedAt: conflict.LocalModifiedAt, createdAt: songDevice.AddedAt, cancellationToken: cancellationToken);
                resolveSyncRecords.Add(tsRecord);
                records.Add(SyncRecordResponseItem.FromEntity(tsRecord));

                logger.LogInformation(
                    "Resolved conflict for {Path} - checksums match, updated LastSyncedModifiedAt to {LastSyncedAt}",
                    conflict.Path, newLastSynced);
            }
            else
            {
                var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;
                var conflictRecord = await syncActions.ActionConflict(conflict.Path, conflict.LocalModifiedAt, songFileModifiedAt, conflict.SongId, "Conflict: local and server both modified, checksums differ", localChecksum: localChecksum, serverChecksum: songDevice.Song.Checksum, algorithm: songDevice.Song.ChecksumAlgorithm, cancellationToken);
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
                    // Use FileModifiedAt (file-content change time) for the new LastSynced
                    // computation so metadata-only edits do not inflate the sync timestamp.
                    var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;

                    var newLastSynced = update.LocalModifiedAt.ToUniversalTime() > songFileModifiedAt
                        ? update.LocalModifiedAt.ToUniversalTime()
                        : songFileModifiedAt;

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
                    var songFileModifiedAt = songDevice.Song.FileModifiedAt ?? songDevice.Song.ModifiedAt;
                    var reason = $"Server modified at {songFileModifiedAt:O} is newer than last synced at {update.LastSyncedAt:O}, checksums differ";
                    var updateRecord = await syncActions.ActionUpdateLocal(updateFilePath, update.SongId, songFileModifiedAt, reason, cancellationToken);
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

    private (string Path, string? PreviousPath) ComputePendingActionPath(
        SongDevice sd, TemplateNamingStrategy namingStrategy, HashSet<string> usedPaths)
        => pathResolver.ComputePendingActionPath(sd, namingStrategy, usedPaths);

    private string GetUniquePath(string basePath, HashSet<string> existingPaths)
        => pathResolver.GetUniquePath(basePath, existingPaths);


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
        var result = await deviceFilterValuesService.GetAsync(currentUser.Id, field, search, limit, cancellationToken);

        return new FilterValuesResponse { Values = result.Values };
    }

    private bool IsNewerThan(DateTime current, DateTime reference)
        => comparisonHelper.IsNewerThan(current, reference);

    private async Task<Device?> FindDeviceAsync(long deviceId, CancellationToken cancellationToken)
        => await deviceLookup.FindDeviceAsync(context, deviceId, currentUser.Id, cancellationToken);

    private async Task<ActionResult<DeviceSyncSession>> GetActiveSessionAsync(long sessionId, long deviceId, CancellationToken cancellationToken)
    {
        var result = await sessionLookup.GetActiveSessionAsync(context, sessionId, deviceId, currentUser.Id, cancellationToken);
        if (result.Found) return result.Session!;
        if (result.Failure == ActiveSessionFailure.NotFound) return NotFound();
        throw new Exception($"Sync session {result.NotInProgressSessionId} is not in progress (status: {result.NotInProgressStatus})");
    }
}
