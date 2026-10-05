using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Albums;

namespace MyMusic.Common.Services.Artists;

/// <summary>
/// Default implementation of <see cref="IArtistRemoveService"/>.
/// </summary>
public class ArtistRemoveService(
    MusicDbContext db,
    IFileTransactionService fileTransactions,
    IAdvisoryLockService advisoryLocks,
    ISongUpdateService songUpdate,
    IAlbumDeleteService albumDelete,
    IArtistDeleteService artistDelete,
    IArtworkDeleteService artworkDelete,
    ILogger<ArtistRemoveService> logger) : IArtistRemoveService
{
    /// <inheritdoc />
    public async Task RemoveAsync(long ownerId, IReadOnlyCollection<long> artistIds,
        CancellationToken cancellationToken = default)
    {
        var ids = artistIds.Distinct().ToArray();

        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);

        var artists = await db.Artists
            .Where(a => ids.Contains(a.Id) && a.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

        if (ids.Except(artists.Select(a => a.Id)).Cast<long?>().FirstOrDefault() is { } missingId)
        {
            throw new ArtistNotFoundException(missingId);
        }

        var artistNames = artists.ToDictionary(a => a.Id, a => a.Name);

        var albums = await db.Albums
            .Where(a => a.OwnerId == ownerId && ids.Contains(a.ArtistId))
            .Select(a => new { a.Id, a.Name, a.ArtistId, a.CoverId })
            .ToListAsync(cancellationToken);

        var songs = (await AlbumArtistSongsQuery.OfArtists(db, ownerId, ids)
                .OrderBy(s => s.Id)
                .Select(s => new
                {
                    s.Id,
                    AlbumArtistId = s.Album.ArtistId,
                    Artists = s.Artists
                        .OrderBy(sa => sa.Id)
                        .Select(sa => new SongArtistInfo(sa.ArtistId, sa.Artist.Name))
                        .ToList(),
                })
                .ToListAsync(cancellationToken))
            .Select(s => new AffectedSong(s.Id, s.AlbumArtistId, s.Artists,
                s.Artists.Where(a => !ids.Contains(a.Id)).ToList()))
            .ToList();

        EnsurePlaceholdersCanBeDeleted(artists, songs);

        // Everything the songs leave and reach: the artists and their albums, the artists that remain, and the
        // placeholder albums (and placeholder artist) that take the songs in
        locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db,
            AlbumArtistLockKeys.Create(ownerId,
                songs.SelectMany(song => song.Remaining.Count > 0
                        ? song.Remaining.Select(a => a.Name)
                        : [Artist.PlaceholderName])
                    .Concat(artistNames.Values),
                albums.Select(album => (artistNames[album.ArtistId], album.Name))
                    .Concat(songs.Where(song => ids.Contains(song.AlbumArtistId))
                        .Select(song => (song.NewAlbumArtistName, Album.PlaceholderName)))),
            cancellationToken);

        // The artists and their albums are deleted below, even when the songs leave some of them with nothing else
        var albumIds = albums.Select(a => a.Id).ToArray();
        await songUpdate.UpdateSongsAsync(db, files,
            songs.Select(song => (song.Id, CreateUpdate(song, ids))),
            new SongUpdateOptions { KeepAlbumIds = albumIds, KeepArtistIds = ids },
            cancellationToken);

        await albumDelete.DeleteAsync(albumIds, cancellationToken);
        await artistDelete.DeleteAsync(ids, cancellationToken);

        // They were deleted directly in the database: stop tracking them
        var deletedEntries = db.ChangeTracker.Entries()
            .Where(entry => entry.Entity is Album album && albumIds.Contains(album.Id)
                            || entry.Entity is Artist artist && ids.Contains(artist.Id))
            .ToList();
        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Detached;
        }

        var artworkIds = albums.Select(a => a.CoverId)
            .Concat(artists.SelectMany(a => new[] { a.PhotoId, a.BackgroundId }))
            .OfType<long>()
            .Distinct()
            .ToArray();
        await artworkDelete.DeleteIfUnusedAsync(artworkIds, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Deleted {ArtistsCount} artists ({ArtistIds}) of user {UserId}, with {AlbumsCount} albums, updating {SongsCount} songs",
            ids.Length, ids, ownerId, albums.Count, songs.Count);
    }

    /// <summary>
    ///     A placeholder artist is where songs without an artist live: it can only go when no song involves it, and
    ///     the other artists being deleted leave no song without artists.
    /// </summary>
    private static void EnsurePlaceholdersCanBeDeleted(List<Artist> artists, List<AffectedSong> songs)
    {
        foreach (var placeholder in artists.Where(a => a.Name == Artist.PlaceholderName))
        {
            if (songs.Any(song => song.AlbumArtistId == placeholder.Id
                                  || song.Artists.Any(a => a.Id == placeholder.Id)))
            {
                throw new ValidationException(
                    $"The artist '{placeholder.Name}' cannot be deleted while it still has songs: they have no other artist to move to");
            }

            if (songs.Any(song => song.Remaining.Count == 0))
            {
                throw new ValidationException(
                    $"The artist '{placeholder.Name}' cannot be deleted along with the other artists: their songs move to it");
            }
        }
    }

    /// <summary>
    ///     Takes the deleted artists out of the song's artists, and moves a song of one of their albums to the
    ///     placeholder album of its first remaining artist.
    /// </summary>
    private static SongUpdateModel CreateUpdate(AffectedSong song, long[] deletedArtistIds)
    {
        var update = new SongUpdateModel();

        if (song.Remaining.Count != song.Artists.Count)
        {
            update.Artists = new ValueUpdate<List<ArtistRef>>(song.Remaining.Count > 0
                ? song.Remaining.Select(a => new ArtistRef(a.Id)).ToList()
                : [new ArtistRef(Name: Artist.PlaceholderName)]);
        }

        if (deletedArtistIds.Contains(song.AlbumArtistId))
        {
            update.Album = new ValueUpdate<AlbumRef>(new AlbumRef(Artist: song.Remaining.Count > 0
                ? new ArtistRef(song.Remaining[0].Id)
                : new ArtistRef(Name: Artist.PlaceholderName)));
        }

        return update;
    }

    private record SongArtistInfo(long Id, string Name);

    /// <param name="Artists">The song's artists, in order.</param>
    /// <param name="Remaining">The song's artists that are not being deleted.</param>
    private record AffectedSong(long Id, long AlbumArtistId, List<SongArtistInfo> Artists,
        List<SongArtistInfo> Remaining)
    {
        public string NewAlbumArtistName => Remaining.Count > 0 ? Remaining[0].Name : Artist.PlaceholderName;
    }
}
