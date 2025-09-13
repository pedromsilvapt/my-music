using System.IO.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("songs")]
public class SongsController(ILogger<SongsController> logger) : ControllerBase
{
    private readonly ILogger<SongsController> _logger = logger;

    [HttpGet(Name = "ListSongs")]
    public async Task<ListSongsResponse> List(MusicDbContext context, CancellationToken cancellationToken)
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

    [HttpGet("{id}/download", Name = "DownloadSong")]
    public async Task<IActionResult> Download(MusicDbContext context, IFileSystem fileSystem, long id,
        CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .SingleAsync(s => s.Id == id, cancellationToken);

        var fileStream = fileSystem.File.OpenRead(songs.RepositoryPath);

        new FileExtensionContentTypeProvider().TryGetContentType(songs.RepositoryPath, out var contentType);

        return File(fileStream, contentType ?? "audio/mpeg", enableRangeProcessing: true,
            fileDownloadName: fileSystem.Path.GetFileName(songs.RepositoryPath));
    }

    [HttpPost("/songs/import", Name = "ImportSongs")]
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