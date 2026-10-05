using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.Filters;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Albums;
using MyMusic.Server.DTO.Albums;
using MyMusic.Server.DTO.Filters;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("albums")]
public class AlbumsController(ILogger<AlbumsController> logger, ICurrentUser currentUser) : ControllerBase
{
    private readonly ILogger<AlbumsController> _logger = logger;

    [HttpGet(Name = "ListAlbums")]
    public async Task<ListAlbumsResponse> List(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null,
        [FromQuery] long? ownerId = null)
    {
        // ownerId null/self → my library (unchanged behavior);
        // ownerId another user → albums that user owns which are linked to ≥1 song shared with me.
        var query = (ownerId is null || ownerId == currentUser.Id
                ? context.Albums.Where(a => a.OwnerId == currentUser.Id)
                : context.Albums.Where(a =>
                    a.OwnerId == ownerId.Value &&
                    a.Songs.Any(s => s.IsSharedWith(currentUser.Id))));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = FuzzySearchHelper.ApplyFuzzySearch(query, search, a => a.SearchableText);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterRequest = FilterDslParser.Parse(filter);
            var filterExpression = DynamicFilterBuilder.BuildFilter<Album>(filterRequest);
            query = query.Where(filterExpression);
        }

        var albums = await query.ToListAsync(cancellationToken);

        return new ListAlbumsResponse
        {
            Albums = albums.Select(ListAlbumItem.FromEntity).ToList(),
        };
    }

    [HttpPost(Name = "CreateAlbum")]
    public async Task<ActionResult<CreateAlbumResponse>> Create(
        [FromBody] CreateAlbumRequest request,
        [FromServices] IAlbumCreateService albumCreateService,
        CancellationToken cancellationToken)
    {
        try
        {
            var album = await albumCreateService.CreateAsync(currentUser.Id,
                new AlbumCreateInput { Name = request.Name, ArtistId = request.ArtistId, Year = request.Year },
                cancellationToken);

            return new CreateAlbumResponse
            {
                Album = new CreateAlbumItem
                {
                    Id = album.Id,
                    Name = album.Name,
                    Year = album.Year,
                    ArtistId = album.ArtistId,
                },
            };
        }
        catch (AlbumAlreadyExistsException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Album already exists");
        }
        catch (ValidationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Album cannot be created");
        }
    }

    [HttpGet("{id:long}", Name = "GetAlbum")]
    public async Task<GetAlbumResponse> Get(long id, MusicDbContext context, CancellationToken cancellationToken)
    {
        // Load the album with sharing rows so the recipient view can trim to shared songs.
        var album = await context.Albums
            .Include(a => a.Artist)
            .IncludeSongMetadata("Songs", includeAlbum: false)
            .Include("Songs.PlaylistSongs.Playlist.PlaylistSharings")
            .FirstOrDefaultAsync(a =>
                a.Id == id &&
                (a.OwnerId == currentUser.Id ||
                 a.Songs.Any(s => s.IsSharedWith(currentUser.Id))),
                cancellationToken);

        if (album == null)
        {
            throw new Exception($"Album not found with id {id}");
        }

        // Recipient view: trim to only songs shared with me. Safe because this GET never calls
        // SaveChanges — reassigning the nav collection on a tracked entity never touches the DB,
        // and the DTO reads the trimmed list. (AsNoTracking can't be used here: the Artist include
        // path creates a cycle Artist→Songs→Song→Artists→Artist, which EF rejects for no-tracking
        // queries.)
        if (album.OwnerId != currentUser.Id)
        {
            album.Songs = album.Songs
                .Where(s => s.IsSharedWith(currentUser.Id))
                .ToList();
        }

        return new GetAlbumResponse
        {
            Album = GetAlbumResponseAlbum.FromEntity(album, currentUser.Id),
        };
    }

    // A POST, so a selection of any size fits: the ids go in the body
    [HttpPost("usage", Name = "GetAlbumsUsage")]
    public async Task<GetAlbumsUsageResponse> GetUsage([FromBody] GetAlbumsUsageRequest request,
        MusicDbContext context, CancellationToken cancellationToken) =>
        new()
        {
            SongsCount = await AlbumArtistSongsQuery.OfAlbums(context, currentUser.Id, request.AlbumIds)
                .CountAsync(cancellationToken),
        };

    [HttpPut(Name = "UpdateAlbums")]
    public async Task<IActionResult> Update([FromBody] UpdateAlbumsRequest request,
        [FromServices] IAlbumEditService albumEditService, CancellationToken cancellationToken)
    {
        try
        {
            await albumEditService.EditAsync(currentUser.Id,
                request.Albums
                    .Select(album => new AlbumEditInput { AlbumId = album.Id, Name = album.Name, Year = album.Year })
                    .ToList(),
                cancellationToken);

            return NoContent();
        }
        catch (AlbumNotFoundException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Album not found");
        }
        catch (AlbumAlreadyExistsException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Album already exists");
        }
        catch (ValidationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest,
                title: "Albums cannot be updated");
        }
    }

    [HttpDelete(Name = "DeleteAlbums")]
    public async Task<IActionResult> Delete([FromBody] DeleteAlbumsRequest request,
        [FromServices] IAlbumRemoveService albumRemoveService, CancellationToken cancellationToken)
    {
        try
        {
            await albumRemoveService.RemoveAsync(currentUser.Id, request.AlbumIds, cancellationToken);

            return NoContent();
        }
        catch (AlbumNotFoundException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Album not found");
        }
        catch (ValidationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict,
                title: "Albums cannot be deleted");
        }
    }

    [HttpGet("filter-metadata", Name = "GetAlbumFilterMetadata")]
    public FilterMetadataResponse GetFilterMetadata() =>
        new()
        {
            Fields =
            [
                new FilterFieldMetadata
                {
                    Name = "name",
                    Type = "string",
                    Description = "Album name",
                    SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
                    SupportsDynamicValues = true,
                },
                new FilterFieldMetadata
                {
                    Name = "year",
                    Type = "number",
                    Description = "Release year",
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "isNull", "isNotNull"],
                },
                new FilterFieldMetadata
                {
                    Name = "songsCount",
                    Type = "number",
                    Description = "Number of songs",
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
                },
                new FilterFieldMetadata
                {
                    Name = "createdAt",
                    Type = "date",
                    Description = "Date created",
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "isNull", "isNotNull"],
                },
                new FilterFieldMetadata
                {
                    Name = "searchableText",
                    Type = "string",
                    Description = "Combined searchable text (name + artist)",
                    IsComputed = true,
                    SupportedOperators = ["contains"],
                },
                new FilterFieldMetadata
                {
                    Name = "totalDurationSeconds",
                    Type = "number",
                    Description = "Total duration in seconds",
                    IsComputed = true,
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
                },
            ],
            Operators = FilterMetadataHelper.GetOperatorMetadata(),
        };

    [HttpGet("filter-values", Name = "GetAlbumFilterValues")]
    public async Task<FilterValuesResponse> GetFilterValues(
        [FromQuery] string field,
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15,
        [FromQuery] long? ownerId = null)
    {
        // Mirror List's ownerId scoping so autocomplete reflects the active view.
        var scoped = ownerId is null || ownerId == currentUser.Id
            ? context.Albums.Where(a => a.OwnerId == currentUser.Id)
            : context.Albums.Where(a =>
                a.OwnerId == ownerId.Value &&
                a.Songs.Any(s => s.IsSharedWith(currentUser.Id)));

        var query = field switch
        {
            "name" => scoped
                .Select(a => a.Name)
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