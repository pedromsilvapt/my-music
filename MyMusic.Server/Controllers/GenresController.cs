using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Genres;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("genres")]
public class GenresController(ILogger<GenresController> logger, ICurrentUser currentUser) : ControllerBase
{
    private readonly ILogger<GenresController> _logger = logger;

    [HttpGet(Name = "ListGenres")]
    public async Task<ListGenresResponse> List(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] long? ownerId = null)
    {
        // ownerId null/self → my library (unchanged behavior);
        // ownerId another user → genres that user owns which are linked to ≥1 song shared with me
        // (path: Genre.Songs is List<SongGenre>, SongGenre.Song is the linked Song).
        var query = (ownerId is null || ownerId == currentUser.Id
                ? context.Genres.Where(g => g.OwnerId == currentUser.Id)
                : context.Genres.Where(g =>
                    g.OwnerId == ownerId.Value &&
                    g.Songs.Any(sg => sg.Song.SongSharings.Any(ss => ss.UserId == currentUser.Id))))
            .Include(g => g.Songs);

        var genres = await query.ToListAsync(cancellationToken);

        return new ListGenresResponse
        {
            Genres = genres.Select(ListGenreItem.FromEntity).ToList(),
        };
    }

    [HttpPost(Name = "CreateGenre")]
    public async Task<CreateGenreResponse> Create(
        [FromBody] CreateGenreRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken)
            ?? throw new Exception("User not found");

        var genre = new Genre
        {
            Name = request.Name,
            Owner = user,
            OwnerId = currentUser.Id,
        };

        context.Genres.Add(genre);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created genre {GenreName} with ID {GenreId} for user {UserId}",
            genre.Name, genre.Id, currentUser.Id);

        return new CreateGenreResponse
        {
            Genre = new CreateGenreItem
            {
                Id = genre.Id,
                Name = genre.Name,
            },
        };
    }
}
