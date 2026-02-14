using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Artists;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("artists")]
public class ArtistsController(ILogger<ArtistsController> logger, ICurrentUser currentUser) : ControllerBase
{
    private readonly ILogger<ArtistsController> _logger = logger;

    [HttpGet(Name = "ListArtists")]
    public async Task<ListArtistsResponse> List(MusicDbContext context, CancellationToken cancellationToken)
    {
        var artists = await context.Artists
            .Where(a => a.OwnerId == currentUser.Id)
            .ToListAsync(cancellationToken);

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
}