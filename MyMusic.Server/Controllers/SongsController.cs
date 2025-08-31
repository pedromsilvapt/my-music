using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class SongsController(ILogger<SongsController> logger) : ControllerBase
{
    private readonly ILogger<SongsController> _logger = logger;

    [HttpGet(Name = "ListSongs")]
    public async Task<ListSongsResponse> Get(MusicDbContext context, CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .Include(s => s.Album)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Genres)
            .ThenInclude(g => g.Genre)
            .OrderBy(s => s.Title)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new ListSongsResponse
        {
            Songs = songs.Select(ListSongsResponse.Song.FromEntity).ToList(),
        };
    }

    [HttpPost("/Songs/Import", Name = "ImportSongs")]
    public async Task<object> Import(
        [FromForm] int userId,
        [FromForm] string sourceFolder,
        [FromServices] IMusicService musicService,
        [FromServices] MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var job = new MusicImportJob(_logger);

        await musicService.ImportRepositorySongs(db, job, userId, @sourceFolder,
            cancellationToken: cancellationToken);

        return new { success = true };
    }
}