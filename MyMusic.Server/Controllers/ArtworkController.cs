using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class ArtworkController
{
    [HttpGet("/Artwork/{id}", Name = "GetArtwork")]
    public async Task<IResult> Get(
        IHttpContextAccessor http,
        [FromServices] MusicDbContext db,
        [FromRoute] long id,
        [FromQuery] int size = -1,
        CancellationToken cancellationToken = default
    )
    {
        var artwork = await db.Artworks.SingleOrDefaultAsync(a => a.Id == id, cancellationToken: cancellationToken);

        if (artwork is null)
        {
            return Results.NotFound();
        }

        var image = ImageBuffer.FromBytes(artwork.Data, artwork.MimeType);

        if (size > 0)
        {
            image = image.ToResized(size);
        }

        http.HttpContext!.Response.Headers.CacheControl = "max-age=86400, public";

        return Results.Bytes(image.Data, image.MimeType);
    }
}