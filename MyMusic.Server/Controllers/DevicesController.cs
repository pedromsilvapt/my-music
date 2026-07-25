using System.Globalization;
using System.IO.Abstractions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.Filters;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
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
    ICurrentUser currentUser,
    MusicDbContext context,
    IConfiguration configuration,
    IFileSystem fileSystem,
    ISyncUploadService syncUploadService,
    IDeviceLookupService deviceLookup,
    ISyncSessionLookupService sessionLookup,
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
