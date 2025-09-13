using Microsoft.AspNetCore.Mvc;
using MyMusic.Common;
using MyMusic.Server.DTO.Playlists;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class PlaylistsController
{
    [HttpGet("{id}", Name = "GetPlaylist")]
    public async Task<GetPlaylistResponse> Get(
        [FromServices] MusicDbContext db,
        [FromRoute] long id,
        CancellationToken cancellationToken = default
    )
    {
        return new GetPlaylistResponse
        {
            Songs = [], // TODO
        };
    }
}