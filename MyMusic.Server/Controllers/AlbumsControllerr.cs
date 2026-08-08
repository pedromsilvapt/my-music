using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.Filters;
using MyMusic.Common.Services;
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
                    a.Songs.Any(s => s.SongSharings.Any(ss => ss.UserId == currentUser.Id))));

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
    public async Task<CreateAlbumResponse> Create(
        [FromBody] CreateAlbumRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken)
            ?? throw new Exception("User not found");

        var artist = await context.Artists
            .FirstOrDefaultAsync(a => a.Id == request.ArtistId && a.OwnerId == currentUser.Id, cancellationToken)
            ?? throw new Exception($"Artist not found with id {request.ArtistId}");

        var album = new Album
        {
            Name = request.Name,
            Artist = artist,
            ArtistId = request.ArtistId,
            Owner = user,
            OwnerId = currentUser.Id,
            Year = request.Year,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };

        context.Albums.Add(album);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created album {AlbumName} with ID {AlbumId} for user {UserId}",
            album.Name, album.Id, currentUser.Id);

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

    [HttpGet("{id:long}", Name = "GetAlbum")]
    public async Task<GetAlbumResponse> Get(long id, MusicDbContext context, CancellationToken cancellationToken)
    {
        // Load the album with sharing rows so the recipient view can trim to shared songs.
        var album = await context.Albums
            .Include(a => a.Artist)
            .IncludeSongMetadata("Songs", includeAlbum: false)
            .Include("Songs.SongSharings")
            .FirstOrDefaultAsync(a =>
                a.Id == id &&
                (a.OwnerId == currentUser.Id ||
                 a.Songs.Any(s => s.SongSharings.Any(ss => ss.UserId == currentUser.Id))),
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
                .Where(s => s.SongSharings.Any(ss => ss.UserId == currentUser.Id))
                .ToList();
        }

        return new GetAlbumResponse
        {
            Album = GetAlbumResponseAlbum.FromEntity(album, currentUser.Id),
        };
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
                a.Songs.Any(s => s.SongSharings.Any(ss => ss.UserId == currentUser.Id)));

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