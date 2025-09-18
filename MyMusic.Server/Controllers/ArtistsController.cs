using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Server.DTO.Artists;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("artists")]
public class ArtistsController(ILogger<ArtistsController> logger) : ControllerBase
{
    private readonly ILogger<ArtistsController> _logger = logger;

    [HttpGet(Name = "ListArtists")]
    public async Task<ListArtistsResponse> List(MusicDbContext context, CancellationToken cancellationToken)
    {
        var artists = await context.Artists
            .ToListAsync(cancellationToken);

        return new ListArtistsResponse
        {
            Artists = artists.Select(ListArtistsItem.FromEntity).ToList(),
        };
    }
}