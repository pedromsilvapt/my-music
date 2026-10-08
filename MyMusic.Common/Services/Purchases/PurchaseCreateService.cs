using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Purchases;

/// <summary>
/// Default implementation of <see cref="IPurchaseCreateService"/>.
/// </summary>
public class PurchaseCreateService(
    MusicDbContext db,
    ICurrentUser currentUser,
    ISourcesService sourcesService) : IPurchaseCreateService
{
    /// <inheritdoc />
    public async Task<PurchasedSong?> CreateAsync(long sourceId, string externalId, long? replaceSongId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;

        if (replaceSongId != null &&
            !await db.Songs.AnyAsync(s => s.Id == replaceSongId && s.OwnerId == userId, cancellationToken))
        {
            return null;
        }

        var source = await sourcesService.GetSourceClientAsync(sourceId, cancellationToken);

        var sourceSong = await source.GetSongAsync(externalId, cancellationToken);

        var artists = string.Join(", ", sourceSong.Artists.Select(a => a.Name));
        var album = sourceSong.Album.Name;

        var purchasedSong = new PurchasedSong
        {
            ExternalId = externalId,
            CreatedAt = DateTime.UtcNow,
            SourceId = sourceId,
            Cover = sourceSong.Cover?.Normal ?? sourceSong.Cover?.Smallest,
            Title = sourceSong.Title,
            SubTitle = sourceSong.Year != null
                ? $"{artists} • {album} • {sourceSong.Year.Value}"
                : $"{artists} • {album}",
            Status = PurchasedSongStatus.Queued,
            Progress = 0,
            UserId = userId,
            SongId = replaceSongId,
            ReplacesSongFile = replaceSongId != null,
        };

        await db.PurchasedSongs.AddAsync(purchasedSong, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return purchasedSong;
    }
}
