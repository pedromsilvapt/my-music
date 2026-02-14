using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Albums;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("albums")]
public class AlbumsController(ILogger<AlbumsController> logger, ICurrentUser currentUser) : ControllerBase
{
    private readonly ILogger<AlbumsController> _logger = logger;

    [HttpGet(Name = "ListAlbums")]
    public async Task<ListAlbumsResponse> List(MusicDbContext context, CancellationToken cancellationToken)
    {
        var albums = await context.Albums
            .Where(a => a.OwnerId == currentUser.Id)
            .ToListAsync(cancellationToken);

        return new ListAlbumsResponse
        {
            Albums = albums.Select(ListAlbumsItem.FromEntity).ToList(),
        };
    }

    [HttpGet("{id:long}", Name = "GetAlbum")]
    public async Task<GetAlbumResponse> Get(long id, MusicDbContext context, CancellationToken cancellationToken)
    {
        var album = await context.Albums
            .Include(a => a.Artist)
            .Include(a => a.Songs)
            .Include(a => a.Songs)
            .ThenInclude(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(s => s.Songs)
            .ThenInclude(s => s.Genres)
            .ThenInclude(s => s.Genre)
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
}