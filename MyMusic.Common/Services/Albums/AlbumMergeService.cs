using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Albums;

/// <summary>
/// Default implementation of <see cref="IAlbumMergeService"/>.
/// </summary>
public class AlbumMergeService(
    MusicDbContext db,
    IFileTransactionService fileTransactions,
    IAdvisoryLockService advisoryLocks,
    ISongUpdateService songUpdate,
    IAlbumDeleteService albumDelete,
    IArtistDeleteService artistDelete,
    IArtworkDeleteService artworkDelete,
    ILogger<AlbumMergeService> logger) : IAlbumMergeService
{
    /// <inheritdoc />
    public async Task MergeAsync(long ownerId, long targetId, IReadOnlyCollection<long> sourceIds,
        CancellationToken cancellationToken = default)
    {
        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);

        var (target, sources) = await LoadAlbumsAsync(ownerId, targetId, sourceIds, cancellationToken);
        var ids = sources.Select(a => a.Id).ToArray();

        // The albums the songs leave and the one they reach, whose album artist some of them gain
        locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db,
            AlbumArtistLockKeys.Create(ownerId, [],
                sources.Append(target).Select(album => (album.Artist.Name, album.Name))),
            cancellationToken);

        var songs = await LoadSongsAsync(ownerId, ids, cancellationToken);

        // The sources are deleted below, even the ones that had no songs to begin with
        await songUpdate.UpdateSongsAsync(db, files,
            songs.Select(song => (song.Id, CreateUpdate(song, target))),
            new SongUpdateOptions { KeepAlbumIds = [.. ids, target.Id] }, cancellationToken);

        await MergeRowsAsync(target, sources, cancellationToken);

        await DeleteArtistsLeftUnusedAsync(target, sources, songs, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Merged {AlbumsCount} albums ({AlbumIds}) of user {UserId} into album {TargetId}, moving {SongsCount} songs",
            ids.Length, ids, ownerId, target.Id, songs.Count);
    }

    /// <inheritdoc />
    public async Task<AlbumMergePreview> PreviewAsync(long ownerId, long targetId,
        IReadOnlyCollection<long> sourceIds, CancellationToken cancellationToken = default)
    {
        var (target, sources) = await LoadAlbumsAsync(ownerId, targetId, sourceIds, cancellationToken);
        var ids = sources.Select(a => a.Id).ToArray();

        var songs = AlbumArtistSongsQuery.OfAlbums(db, ownerId, ids);

        return new AlbumMergePreview(
            await songs.CountAsync(cancellationToken),
            await songs.CountAsync(s => s.Artists.All(sa => sa.ArtistId != target.ArtistId), cancellationToken),
            target.Artist.Name);
    }

    /// <inheritdoc />
    public async Task MergeRowsAsync(Album target, IReadOnlyList<Album> sources,
        CancellationToken cancellationToken = default)
    {
        var ids = sources.Select(a => a.Id).ToArray();

        // The first source that has what the target is missing gives it
        target.CoverId ??= sources.Select(a => a.CoverId).FirstOrDefault(coverId => coverId is not null);
        target.Year ??= sources.Select(a => a.Year).FirstOrDefault(year => year is not null);

        // The target takes the links to external sources it does not have yet: the others go with their albums
        var links = await db.AlbumSources
            .Where(link => link.AlbumId == target.Id || ids.Contains(link.AlbumId))
            .OrderBy(link => link.Id)
            .ToListAsync(cancellationToken);
        var known = links
            .Where(link => link.AlbumId == target.Id)
            .Select(link => (link.SourceId, link.ExternalId))
            .ToHashSet();
        foreach (var link in links
                     .Where(link => link.AlbumId != target.Id)
                     .OrderBy(link => Array.IndexOf(ids, link.AlbumId)))
        {
            if (known.Add((link.SourceId, link.ExternalId)))
            {
                link.AlbumId = target.Id;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        await albumDelete.DeleteAsync(ids, cancellationToken);

        // They were deleted directly in the database: stop tracking them
        foreach (var link in links.Where(link => ids.Contains(link.AlbumId)))
        {
            db.Entry(link).State = EntityState.Detached;
        }

        foreach (var source in sources)
        {
            db.Entry(source).State = EntityState.Detached;
        }

        await artworkDelete.DeleteIfUnusedAsync(
            sources.Select(a => a.CoverId).OfType<long>().Distinct().ToArray(), cancellationToken);
    }

    /// <summary>
    ///     Like a song edit, the merge deletes the album artists that the songs moved away from, once nothing else
    ///     uses them. The artist of an album that had no songs is left alone.
    /// </summary>
    private async Task DeleteArtistsLeftUnusedAsync(Album target, List<Album> sources, List<AffectedSong> songs,
        CancellationToken cancellationToken)
    {
        var albumIdsWithSongs = songs.Select(song => song.AlbumId).ToHashSet();
        var artists = sources
            .Where(a => albumIdsWithSongs.Contains(a.Id) && a.ArtistId != target.ArtistId)
            .Select(a => a.Artist)
            .Distinct()
            .ToList();

        var deletedIds = await artistDelete.DeleteIfUnusedAsync(artists.Select(a => a.Id).ToArray(),
            cancellationToken);
        if (deletedIds.Length == 0)
        {
            return;
        }

        var deleted = artists.Where(a => deletedIds.Contains(a.Id)).ToList();
        foreach (var artist in deleted)
        {
            db.Entry(artist).State = EntityState.Detached;
        }

        await artworkDelete.DeleteIfUnusedAsync(
            deleted.SelectMany(a => new[] { a.PhotoId, a.BackgroundId }).OfType<long>().Distinct().ToArray(),
            cancellationToken);
    }

    /// <summary>Loads the target and the albums to merge into it, in the order they were given.</summary>
    private async Task<(Album Target, List<Album> Sources)> LoadAlbumsAsync(long ownerId, long targetId,
        IReadOnlyCollection<long> sourceIds, CancellationToken cancellationToken)
    {
        var ids = sourceIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            throw new ValidationException("There are no albums to merge");
        }

        if (ids.Contains(targetId))
        {
            throw new ValidationException("An album cannot be merged into itself");
        }

        var albums = await db.Albums
            .Include(a => a.Artist)
            .Where(a => (a.Id == targetId || ids.Contains(a.Id)) && a.OwnerId == ownerId)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        if (ids.Prepend(targetId).Except(albums.Keys).Cast<long?>().FirstOrDefault() is { } missingId)
        {
            throw new AlbumNotFoundException(missingId);
        }

        var sources = ids.Select(id => albums[id]).ToList();

        if (sources.FirstOrDefault(a => a.Name == Album.PlaceholderName) is { } placeholder)
        {
            throw new ValidationException(
                $"The album '{placeholder.Name}' of '{placeholder.Artist.Name}' cannot be merged into another album: it holds the songs that have no album");
        }

        return (albums[targetId], sources);
    }

    private async Task<List<AffectedSong>> LoadSongsAsync(long ownerId, long[] albumIds,
        CancellationToken cancellationToken) =>
        (await AlbumArtistSongsQuery.OfAlbums(db, ownerId, albumIds)
            .OrderBy(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.AlbumId,
                ArtistIds = s.Artists.OrderBy(sa => sa.Id).Select(sa => sa.ArtistId).ToList(),
            })
            .ToListAsync(cancellationToken))
        .Select(s => new AffectedSong(s.Id, s.AlbumId, s.ArtistIds))
        .ToList();

    /// <summary>
    ///     Moves the song to the target album, adding the target's album artist to a song that does not have it: the
    ///     album artist is always one of the song's artists.
    /// </summary>
    private static SongUpdateModel CreateUpdate(AffectedSong song, Album target)
    {
        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef { Id = target.Id }),
        };

        if (!song.ArtistIds.Contains(target.ArtistId))
        {
            update.Artists = new ValueUpdate<List<ArtistRef>>(
                song.ArtistIds.Append(target.ArtistId).Select(id => new ArtistRef(id)).ToList());
        }

        return update;
    }

    /// <param name="ArtistIds">The ids of the song's artists, in order.</param>
    private record AffectedSong(long Id, long AlbumId, List<long> ArtistIds);
}
