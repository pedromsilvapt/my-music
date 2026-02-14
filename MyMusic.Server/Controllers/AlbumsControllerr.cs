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
}