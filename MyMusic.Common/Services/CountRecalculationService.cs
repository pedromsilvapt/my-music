using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyMusic.Common.Services;

public class CountRecalculationService(
    MusicDbContext db,
    ILogger<CountRecalculationService> logger) : ICountRecalculationService
{
    public async Task<int> RecalculateAlbumSongCountsAsync(CancellationToken cancellationToken = default)
    {
        var updated = await db.Albums
            .ExecuteUpdateAsync(setter => setter.SetProperty(
                a => a.SongsCount,
                a => db.Songs.Count(s => s.AlbumId == a.Id)),
                cancellationToken);

        logger.LogInformation("Recalculated SongsCount for {Count} albums", updated);
        return updated;
    }

    public async Task<int> RecalculateArtistSongCountsAsync(CancellationToken cancellationToken = default)
    {
        var updated = await db.Artists
            .ExecuteUpdateAsync(setter => setter.SetProperty(
                a => a.SongsCount,
                a => db.SongArtists.Count(sa => sa.ArtistId == a.Id)),
                cancellationToken);

        logger.LogInformation("Recalculated SongsCount for {Count} artists", updated);
        return updated;
    }

    public async Task<int> RecalculateArtistAlbumCountsAsync(CancellationToken cancellationToken = default)
    {
        var updated = await db.Artists
            .ExecuteUpdateAsync(setter => setter.SetProperty(
                a => a.AlbumsCount,
                a => db.Albums.Count(al => al.ArtistId == a.Id)),
                cancellationToken);

        logger.LogInformation("Recalculated AlbumsCount for {Count} artists", updated);
        return updated;
    }

    public async Task<RecalculateCountsResult> RecalculateAllCountsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting full count recalculation");

        var albumsUpdated = await RecalculateAlbumSongCountsAsync(cancellationToken);
        var artistsSongsUpdated = await RecalculateArtistSongCountsAsync(cancellationToken);
        var artistsAlbumsUpdated = await RecalculateArtistAlbumCountsAsync(cancellationToken);

        logger.LogInformation(
            "Completed count recalculation: {AlbumsUpdated} albums, {ArtistsSongsUpdated} artists (songs), {ArtistsAlbumsUpdated} artists (albums)",
            albumsUpdated, artistsSongsUpdated, artistsAlbumsUpdated);

        return new RecalculateCountsResult
        {
            AlbumsUpdated = albumsUpdated,
            ArtistsSongsUpdated = artistsSongsUpdated,
            ArtistsAlbumsUpdated = artistsAlbumsUpdated,
        };
    }
}