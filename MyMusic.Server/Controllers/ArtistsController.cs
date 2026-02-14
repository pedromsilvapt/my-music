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
}