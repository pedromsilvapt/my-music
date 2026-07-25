using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.DTO.Filters;
using MyMusic.Server.DTO.Sync;

namespace MyMusic.Server.Controllers;

/// <summary>
/// Sync session lifecycle endpoints (list, records, filters, delete, prune). Lives under the
/// <c>devices</c> route prefix so session endpoints keep their existing paths
/// (<c>/devices/{deviceId}/sessions</c>, ...). Extracted from <see cref="DevicesController"/>
/// as part of the controllers refactor (Phase 5+).
/// </summary>
[ApiController]
[Route("devices")]
public class SyncSessionsController(
    ILogger<SyncSessionsController> logger,
    ICurrentUser currentUser,
    ISyncSessionListService sessionListService,
    ISyncSessionRecordsQueryService sessionRecordsQueryService,
    ISyncSessionFilterValuesService sessionFilterValuesService) : ControllerBase
{
    [HttpGet("{deviceId:long}/sessions")]
    public async Task<ActionResult<ListSyncSessionsResponse>> ListSessions(
        long deviceId,
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        var result = await sessionListService.ListAsync(deviceId, currentUser.Id, count, cancellationToken);
        if (result == null) return NotFound();

        return new ListSyncSessionsResponse
        {
            Sessions = result.Sessions.Select(SyncSessionItem.FromEntity).ToList(),
        };
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
        var result = await sessionRecordsQueryService.QueryAsync(
            sessionId,
            deviceId,
            currentUser.Id,
            actions,
            limit,
            offset,
            sort,
            includeSongInfo,
            filter,
            cancellationToken);
        if (result == null) return NotFound();

        return new ListSyncRecordsResponse
        {
            Records = result.Records.Select(r => SyncRecordResponseItem.FromEntity(r, includeSongInfo == true)).ToList(),
            NextCursor = result.NextCursor,
            HasMore = result.HasMore,
            TotalCount = result.TotalCount,
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
        var result = await sessionFilterValuesService.GetAsync(
            currentUser.Id,
            deviceId,
            sessionId,
            field,
            search,
            limit,
            cancellationToken);

        return new FilterValuesResponse { Values = result.Values };
    }
}