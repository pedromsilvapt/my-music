using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
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
        [FromQuery] string? filter = null,
        [FromQuery] long? ownerId = null)
    {
        // ownerId null/self → my library (unchanged behavior);
        // ownerId another user → artists that user owns which are linked to ≥1 song shared with me
        // (path: Artist.Songs is List<SongArtist>, SongArtist.Song is the linked Song).
        var query = (ownerId is null || ownerId == currentUser.Id
                ? context.Artists.Where(a => a.OwnerId == currentUser.Id)
                : context.Artists.Where(a =>
                    a.OwnerId == ownerId.Value &&
                    a.Songs.Any(sa => sa.Song.SongSharings.Any(ss => ss.UserId == currentUser.Id))));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = FuzzySearchHelper.ApplyFuzzySearch(query, search, a => a.SearchableText);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<Artist>(filter);
            query = query.Where(filterExpression);
        }

        var artists = await query.ToListAsync(cancellationToken);

        return new ListArtistsResponse
        {
            Artists = artists.Select(ListArtistItem.FromEntity).ToList(),
        };
    }

    [HttpPost(Name = "CreateArtist")]
    public async Task<CreateArtistResponse> Create(
        [FromBody] CreateArtistRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken)
            ?? throw new Exception("User not found");

        var artist = new Artist
        {
            Name = request.Name,
            Owner = user,
            OwnerId = currentUser.Id,
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };

        context.Artists.Add(artist);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created artist {ArtistName} with ID {ArtistId} for user {UserId}",
            artist.Name, artist.Id, currentUser.Id);

        return new CreateArtistResponse
        {
            Artist = new CreateArtistItem
            {
                Id = artist.Id,
                Name = artist.Name,
            },
        };
    }

    [HttpGet("{id:long}", Name = "GetArtist")]
    public async Task<GetArtistResponse> Get(long id, [FromQuery] ArtistSongFilter songFilter, MusicDbContext context,
        CancellationToken cancellationToken)
    {
        // Load the artist with sharing rows so the recipient view can trim to shared songs/albums.
        var artist = await context.Artists
            .Include(a => a.Albums)
            .IncludeSongMetadata("Songs.Song")
            .Include("Songs.Song.SongSharings")
            .FirstOrDefaultAsync(a =>
                a.Id == id &&
                (a.OwnerId == currentUser.Id ||
                 a.Songs.Any(sa => sa.Song.SongSharings.Any(ss => ss.UserId == currentUser.Id))),
                cancellationToken);

        if (artist == null)
        {
            throw new Exception($"Artist not found with id {id}");
        }

        // Recipient view: trim to only songs shared with me, and the albums linked to those songs.
        // Safe because this GET never calls SaveChanges — reassigning the nav collections on a
        // tracked entity never touches the DB, and the DTO reads the trimmed lists. (AsNoTracking
        // can't be used here: the Artist include path creates a cycle Artist→Songs→Song→Artists→Artist,
        // which EF rejects for no-tracking queries.)
        if (artist.OwnerId != currentUser.Id)
        {
            artist.Songs = artist.Songs
                .Where(sa => sa.Song.SongSharings.Any(ss => ss.UserId == currentUser.Id))
                .ToList();
            var accessibleAlbumIds = artist.Songs.Select(sa => sa.Song.AlbumId).ToHashSet();
            artist.Albums = artist.Albums.Where(a => accessibleAlbumIds.Contains(a.Id)).ToList();
        }

        return new GetArtistResponse
        {
            Artist = GetArtistResponseArtist.FromEntity(artist, songFilter, currentUser.Id),
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
        [FromQuery] int limit = 15,
        [FromQuery] long? ownerId = null)
    {
        // Mirror List's ownerId scoping so autocomplete reflects the active view.
        var scoped = ownerId is null || ownerId == currentUser.Id
            ? context.Artists.Where(a => a.OwnerId == currentUser.Id)
            : context.Artists.Where(a =>
                a.OwnerId == ownerId.Value &&
                a.Songs.Any(sa => sa.Song.SongSharings.Any(ss => ss.UserId == currentUser.Id)));

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