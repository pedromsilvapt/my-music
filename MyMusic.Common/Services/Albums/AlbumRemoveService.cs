using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Albums;

/// <summary>
/// Default implementation of <see cref="IAlbumRemoveService"/>.
/// </summary>
public class AlbumRemoveService(
    MusicDbContext db,
    IFileTransactionService fileTransactions,
    IAdvisoryLockService advisoryLocks,
    ISongUpdateService songUpdate,
    IAlbumDeleteService albumDelete,
    IArtworkDeleteService artworkDelete,
    ILogger<AlbumRemoveService> logger) : IAlbumRemoveService
{
    /// <inheritdoc />
    public async Task RemoveAsync(long ownerId, IReadOnlyCollection<long> albumIds,
        CancellationToken cancellationToken = default)
    {
        var ids = albumIds.Distinct().ToArray();

        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);

        var albums = await db.Albums
            .Include(a => a.Artist)
            .Where(a => ids.Contains(a.Id) && a.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        if (ids.Except(albums.Select(a => a.Id)).Cast<long?>().FirstOrDefault() is { } missingId)
        {
            throw new AlbumNotFoundException(missingId);
        }

        // The albums being deleted, and the placeholder albums their songs move to
        locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db,
            AlbumArtistLockKeys.Create(ownerId, [],
                albums.SelectMany(album => new[]
                {
                    (album.Artist.Name, album.Name), (album.Artist.Name, Album.PlaceholderName),
                })),
            cancellationToken);

        var songs = await AlbumArtistSongsQuery.OfAlbums(db, ownerId, ids)
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.AlbumId })
            .ToListAsync(cancellationToken);

        EnsurePlaceholdersCanBeDeleted(albums, songs.Select(s => s.AlbumId).ToHashSet());

        // The albums are deleted below, even the ones that had no songs to begin with
        await songUpdate.UpdateSongsAsync(db, files,
            songs.Select(song => (song.Id, new SongUpdateModel { Album = new ValueUpdate<AlbumRef>() })),
            new SongUpdateOptions { KeepAlbumIds = ids }, cancellationToken);

        await albumDelete.DeleteAsync(ids, cancellationToken);
        foreach (var album in albums)
        {
            db.Entry(album).State = EntityState.Detached;
        }

        await artworkDelete.DeleteIfUnusedAsync(
            albums.Select(a => a.CoverId).OfType<long>().Distinct().ToArray(), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Deleted {AlbumsCount} albums ({AlbumIds}) of user {UserId}, moving {SongsCount} songs",
            ids.Length, ids, ownerId, songs.Count);
    }

    /// <summary>
    ///     A placeholder album is where songs without an album live: it can only go when it has no songs, and none
    ///     of the other albums being deleted sends its songs there.
    /// </summary>
    private static void EnsurePlaceholdersCanBeDeleted(List<Album> albums, HashSet<long> albumIdsWithSongs)
    {
        foreach (var placeholder in albums.Where(a => a.Name == Album.PlaceholderName))
        {
            if (albumIdsWithSongs.Contains(placeholder.Id))
            {
                throw new ValidationException(
                    $"The album '{placeholder.Name}' of '{placeholder.Artist.Name}' cannot be deleted while it still has songs: they have no other album to move to");
            }

            if (albums.Any(a => a.ArtistId == placeholder.ArtistId && albumIdsWithSongs.Contains(a.Id)))
            {
                throw new ValidationException(
                    $"The album '{placeholder.Name}' of '{placeholder.Artist.Name}' cannot be deleted along with the other albums: their songs move to it");
            }
        }
    }
}
