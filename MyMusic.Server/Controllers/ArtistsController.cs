using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Filters;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Artists;
using MyMusic.Server.DTO.Filters;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("artists")]
public class ArtistsController(ILogger<ArtistsController> logger, ICurrentUser currentUser) : ControllerBase
{
    private readonly ILogger<ArtistsController> _logger = logger;

    [HttpGet(Name = "ListArtists")]
    public async Task<ListArtistsResponse> List(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null)
    {
        var query = context.Artists
            .Where(a => a.OwnerId == currentUser.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = FuzzySearchHelper.ApplyFuzzySearch(query, search, a => a.SearchableText);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterRequest = FilterDslParser.Parse(filter);
            var filterExpression = DynamicFilterBuilder.BuildFilter<Artist>(filterRequest);
            query = query.Where(filterExpression);
        }

        var artists = await query.ToListAsync(cancellationToken);

        return new ListArtistsResponse
        {
            Artists = artists.Select(ListArtistsItem.FromEntity).ToList(),
        };
    }

    [HttpGet("{id:long}", Name = "GetArtist")]
    public async Task<GetArtistResponse> Get(long id, [FromQuery] ArtistSongFilter songFilter, MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var artist = await context.Artists
            .Include(a => a.Albums)
            .Include(a => a.Songs)
            .ThenInclude(sa => sa.Song)
            .ThenInclude(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(a => a.Songs)
            .ThenInclude(sa => sa.Song)
            .ThenInclude(s => s.Album)
            .ThenInclude(s => s.Artist)
            .Include(a => a.Songs)
            .ThenInclude(s => s.Song)
            .ThenInclude(s => s.Genres)
            .ThenInclude(s => s.Genre)
            .FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == currentUser.Id, cancellationToken);

        if (artist == null)
        {
            throw new Exception($"Artist not found with id {id}");
        }

        return new GetArtistResponse
        {
            Artist = GetArtistResponseArtist.FromEntity(artist, songFilter),
        };
    }

    [HttpGet("filter-metadata", Name = "GetArtistFilterMetadata")]
    public FilterMetadataResponse GetFilterMetadata()
    {
        var operators = FilterMetadataHelper.GetOperatorMetadata();
        var fields = GetArtistFieldMetadata();

        return new FilterMetadataResponse
        {
            Fields = fields,
            Operators = operators,
        };
    }

    [HttpGet("filter-values", Name = "GetArtistFilterValues")]
    public async Task<FilterValuesResponse> GetFilterValues(
        [FromQuery] string field,
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = field switch
        {
            "name" => context.Artists
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

    private static List<FilterFieldMetadata> GetArtistFieldMetadata() =>
    [
        new()
        {
            Name = "name",
            Type = "string",
            Description = "Artist name",
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
            SupportsDynamicValues = true,
        },
        new()
        {
            Name = "songsCount",
            Type = "number",
            Description = "Number of songs",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
        },
        new()
        {
            Name = "albumsCount",
            Type = "number",
            Description = "Number of albums",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
        },
        new()
        {
            Name = "createdAt",
            Type = "date",
            Description = "Date created",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "isNull", "isNotNull"],
        },
        new()
        {
            Name = "searchableText",
            Type = "string",
            Description = "Combined searchable text",
            IsComputed = true,
            SupportedOperators = ["contains"],
        },
    ];
}