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
public class SongsController(ILogger<SongsController> logger, ICurrentUser currentUser) : ControllerBase
{
    private readonly ILogger<SongsController> _logger = logger;

    [HttpGet(Name = "ListSongs")]
    public async Task<ListSongsResponse> List(MusicDbContext context, CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .Where(s => s.OwnerId == currentUser.Id)
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
            Songs = songs.Select(ListSongsItem.FromEntity).ToList(),
        };
    }

    [HttpGet("{id:long}", Name = "GetSong")]
    public async Task<GetSongResponse> Get(long id, MusicDbContext context, CancellationToken cancellationToken)
    {
        var song = await context.Songs
            .Where(s => s.Id == id && s.OwnerId == currentUser.Id)
            .Include(s => s.Album)
            .Include(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Genres)
            .ThenInclude(g => g.Genre)
            .FirstOrDefaultAsync(cancellationToken);

        if (song == null)
        {
            throw new Exception($"Song not found with id {id}");
        }

        return new GetSongResponse
        {
            Song = GetSongResponseSong.FromEntity(song),
        };
    }

    [HttpGet("{id}/download", Name = "DownloadSong")]
    public async Task<IActionResult> Download(MusicDbContext context, IFileSystem fileSystem, long id,
        CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .SingleAsync(s => s.Id == id && s.OwnerId == currentUser.Id, cancellationToken);

        var fileStream = fileSystem.File.OpenRead(songs.RepositoryPath);

        new FileExtensionContentTypeProvider().TryGetContentType(songs.RepositoryPath, out var contentType);

        return File(fileStream, contentType ?? "audio/mpeg", enableRangeProcessing: true,
            fileDownloadName: fileSystem.Path.GetFileName(songs.RepositoryPath));
    }

    [HttpPost("import", Name = "ImportSongs")]
    public async Task<object> Import(
        [FromForm] string sourceFolder,
        [FromServices] IMusicService musicService,
        [FromServices] MusicImportJob job,
        [FromServices] MusicDbContext db,
        CancellationToken cancellationToken)
    {
        await musicService.ImportRepositorySongs(db, job, currentUser.Id, sourceFolder,
            cancellationToken: cancellationToken);

        return new { success = true };
    }

    [HttpPost("{id:long}/favorite", Name = "ToggleSongFavorite")]
    public async Task<ToggleFavoriteResponse> ToggleFavorite(long id, MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var song = await context.Songs
            .Where(s => s.Id == id && s.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (song == null)
        {
            throw new Exception($"Song not found with id {id}");
        }

        song.IsFavorite = !song.IsFavorite;
        await context.SaveChangesAsync(cancellationToken);

        return new ToggleFavoriteResponse
        {
            IsFavorite = song.IsFavorite
        };
    }

    [HttpPost("favorites", Name = "ToggleFavorites")]
    public async Task<ToggleFavoritesResponse> ToggleFavorites([FromBody] ToggleFavoritesRequest request,
        MusicDbContext context, CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .Where(s => request.Ids.Contains(s.Id) && s.OwnerId == currentUser.Id)
            .ToListAsync(cancellationToken);

        var songDict = songs.ToDictionary(s => s.Id);
        var result = new List<ToggleFavoriteItem>();

        foreach (var id in request.Ids)
        {
            if (songDict.TryGetValue(id, out var song))
            {
                song.IsFavorite = !song.IsFavorite;
                result.Add(new ToggleFavoriteItem { Id = id, IsFavorite = song.IsFavorite });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return new ToggleFavoritesResponse
        {
            Songs = result
        };
    }
}