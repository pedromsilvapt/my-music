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
        [FromQuery] string? filter = null)
    {
        var query = context.Albums
            .Where(a => a.OwnerId == currentUser.Id);

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
        var album = await context.Albums
            .Include(a => a.Artist)
            .IncludeSongMetadata("Songs", includeAlbum: false)
            .FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == currentUser.Id, cancellationToken);

        if (album == null)
        {
            throw new Exception($"Album not found with id {id}");
        }

        return new GetAlbumResponse
        {
            Album = GetAlbumResponseAlbum.FromEntity(album),
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
        [FromQuery] int limit = 15)
    {
        var query = field switch
        {
            "name" => context.Albums
                .Where(a => a.OwnerId == currentUser.Id)
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