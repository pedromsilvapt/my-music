using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Albums;

namespace MyMusic.Common.Services.Artists;

/// <summary>
/// Default implementation of <see cref="IArtistMergeService"/>.
/// </summary>
public class ArtistMergeService(
    MusicDbContext db,
    IFileTransactionService fileTransactions,
    IAdvisoryLockService advisoryLocks,
    ISongUpdateService songUpdate,
    IAlbumMergeService albumMerge,
    IArtistDeleteService artistDelete,
    IArtworkDeleteService artworkDelete,
    ILogger<ArtistMergeService> logger) : IArtistMergeService
{
    /// <inheritdoc />
    public async Task MergeAsync(long ownerId, long targetId, IReadOnlyCollection<long> sourceIds,
        CancellationToken cancellationToken = default)
    {
        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);

        var (target, sources) = await LoadArtistsAsync(ownerId, targetId, sourceIds, cancellationToken);
        var ids = sources.Select(a => a.Id).ToArray();
        var albums = await PlanAlbumsAsync(ownerId, target, sources, cancellationToken);

        // The artists and their albums, under the names they have and the ones they get
        locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db,
            AlbumArtistLockKeys.Create(ownerId,
                sources.Append(target).Select(artist => artist.Name),
                albums.SelectMany(group => group.Sources).SelectMany(album => new[]
                {
                    (album.Artist.Name, album.Name), (target.Name, album.Name),
                })),
            cancellationToken);

        var songs = await LoadSongsAsync(ownerId, ids, cancellationToken);

        // An album that stays becomes an album of the target, before its songs are rewritten
        foreach (var album in albums.Select(group => group.Kept).Where(album => album.ArtistId != target.Id))
        {
            album.Artist = target;
            album.ArtistId = target.Id;
        }

        await db.SaveChangesAsync(cancellationToken);

        // The artists and the albums merged into another one are deleted below
        var sourceAlbumIds = albums.SelectMany(group => group.Sources).Select(album => album.Id).ToArray();
        var keptAlbumIds = albums
            .SelectMany(group => group.Merged.Select(album => (album.Id, Kept: group.Kept.Id)))
            .ToDictionary(pair => pair.Id, pair => pair.Kept);
        await songUpdate.UpdateSongsAsync(db, files,
            songs.Select(song => (song.Id, CreateUpdate(song, target.Id, ids, sourceAlbumIds, keptAlbumIds))),
            new SongUpdateOptions
            {
                KeepAlbumIds = [.. sourceAlbumIds, .. keptAlbumIds.Values],
                KeepArtistIds = [.. ids, target.Id],
            },
            cancellationToken);

        foreach (var group in albums.Where(group => group.Merged.Count > 0))
        {
            await albumMerge.MergeRowsAsync(group.Kept, group.Merged, cancellationToken);
        }

        await MergeRowsAsync(target, sources, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Merged {ArtistsCount} artists ({ArtistIds}) of user {UserId} into artist {TargetId}, with {AlbumsCount} albums ({MergedAlbumsCount} merged), updating {SongsCount} songs",
            ids.Length, ids, ownerId, target.Id, sourceAlbumIds.Length, keptAlbumIds.Count, songs.Count);
    }

    /// <inheritdoc />
    public async Task<ArtistMergePreview> PreviewAsync(long ownerId, long targetId,
        IReadOnlyCollection<long> sourceIds, CancellationToken cancellationToken = default)
    {
        var (target, sources) = await LoadArtistsAsync(ownerId, targetId, sourceIds, cancellationToken);
        var ids = sources.Select(a => a.Id).ToArray();
        var albums = await PlanAlbumsAsync(ownerId, target, sources, cancellationToken);

        return new ArtistMergePreview(
            await AlbumArtistSongsQuery.OfArtists(db, ownerId, ids).CountAsync(cancellationToken),
            albums.Sum(group => group.Merged.Count));
    }

    /// <summary>Loads the target and the artists to merge into it, in the order they were given.</summary>
    private async Task<(Artist Target, List<Artist> Sources)> LoadArtistsAsync(long ownerId, long targetId,
        IReadOnlyCollection<long> sourceIds, CancellationToken cancellationToken)
    {
        var ids = sourceIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            throw new ValidationException("There are no artists to merge");
        }

        if (ids.Contains(targetId))
        {
            throw new ValidationException("An artist cannot be merged into itself");
        }

        var artists = await db.Artists
            .Where(a => (a.Id == targetId || ids.Contains(a.Id)) && a.OwnerId == ownerId)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        if (ids.Prepend(targetId).Except(artists.Keys).Cast<long?>().FirstOrDefault() is { } missingId)
        {
            throw new ArtistNotFoundException(missingId);
        }

        var sources = ids.Select(id => artists[id]).ToList();

        if (sources.FirstOrDefault(a => a.Name == Artist.PlaceholderName) is { } placeholder)
        {
            throw new ValidationException(
                $"The artist '{placeholder.Name}' cannot be merged into another artist: it holds the songs that have no artist");
        }

        return (artists[targetId], sources);
    }

    /// <summary>
    ///     Decides what becomes of the albums of the artists being merged. Album names are unique per artist, so the
    ///     albums sharing a name end up as a single one: the album the target already has, or else the album of the
    ///     first artist that has one, which stays and is given to the target.
    /// </summary>
    private async Task<List<AlbumGroup>> PlanAlbumsAsync(long ownerId, Artist target, List<Artist> sources,
        CancellationToken cancellationToken)
    {
        var ids = sources.Select(a => a.Id).ToArray();

        // The artists are tracked: each album gets its artist without loading it again
        var sourceAlbums = (await db.Albums
                .Where(a => a.OwnerId == ownerId && ids.Contains(a.ArtistId))
                .ToListAsync(cancellationToken))
            .OrderBy(a => Array.IndexOf(ids, a.ArtistId))
            .ThenBy(a => a.Id)
            .ToList();

        var names = sourceAlbums.Select(a => a.Name).Distinct().ToArray();
        var targetAlbums = await db.Albums
            .Where(a => a.OwnerId == ownerId && a.ArtistId == target.Id && names.Contains(a.Name))
            .ToDictionaryAsync(a => a.Name, cancellationToken);

        return sourceAlbums
            .GroupBy(a => a.Name)
            .Select(group =>
            {
                var kept = targetAlbums.GetValueOrDefault(group.Key) ?? group.First();

                return new AlbumGroup(kept, group.ToList(), group.Where(a => a != kept).ToList());
            })
            .ToList();
    }

    private async Task<List<AffectedSong>> LoadSongsAsync(long ownerId, long[] artistIds,
        CancellationToken cancellationToken) =>
        (await AlbumArtistSongsQuery.OfArtists(db, ownerId, artistIds)
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
    ///     Puts the target in the place of the merged artists among the song's artists, once, and moves a song of
    ///     an album that is merged into another one to that album. A song of any of the merged artists' albums ends
    ///     in an album of the target, so it gains the target when it does not have it: the album artist is always
    ///     one of the song's artists.
    /// </summary>
    private static SongUpdateModel CreateUpdate(AffectedSong song, long targetId, long[] mergedArtistIds,
        long[] sourceAlbumIds, Dictionary<long, long> keptAlbumIds)
    {
        var update = new SongUpdateModel();

        var artistIds = song.ArtistIds
            .Select(id => mergedArtistIds.Contains(id) ? targetId : id)
            .Distinct()
            .ToList();
        if (sourceAlbumIds.Contains(song.AlbumId) && !artistIds.Contains(targetId))
        {
            artistIds.Add(targetId);
        }

        if (!artistIds.SequenceEqual(song.ArtistIds))
        {
            update.Artists = new ValueUpdate<List<ArtistRef>>(artistIds.Select(id => new ArtistRef(id)).ToList());
        }

        if (keptAlbumIds.TryGetValue(song.AlbumId, out var keptAlbumId))
        {
            update.Album = new ValueUpdate<AlbumRef>(new AlbumRef { Id = keptAlbumId });
        }

        return update;
    }

    /// <summary>
    ///     The target takes the merged artists' links to external sources, and their photo and background when it
    ///     has none; the merged artists, which no song and no album references anymore, are then deleted.
    /// </summary>
    private async Task MergeRowsAsync(Artist target, List<Artist> sources, CancellationToken cancellationToken)
    {
        var ids = sources.Select(a => a.Id).ToArray();

        // The first artist that has what the target is missing gives it
        target.PhotoId ??= sources.Select(a => a.PhotoId).FirstOrDefault(photoId => photoId is not null);
        target.BackgroundId ??= sources.Select(a => a.BackgroundId)
            .FirstOrDefault(backgroundId => backgroundId is not null);

        // The target takes the links to external sources it does not have yet: the others go with their artists
        var links = await db.ArtistSources
            .Where(link => link.ArtistId == target.Id || ids.Contains(link.ArtistId))
            .OrderBy(link => link.Id)
            .ToListAsync(cancellationToken);
        var known = links
            .Where(link => link.ArtistId == target.Id)
            .Select(link => (link.SourceId, link.ExternalId))
            .ToHashSet();
        foreach (var link in links
                     .Where(link => link.ArtistId != target.Id)
                     .OrderBy(link => Array.IndexOf(ids, link.ArtistId)))
        {
            if (known.Add((link.SourceId, link.ExternalId)))
            {
                link.ArtistId = target.Id;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        await artistDelete.DeleteAsync(ids, cancellationToken);

        // They were deleted directly in the database: stop tracking them
        foreach (var link in links.Where(link => ids.Contains(link.ArtistId)))
        {
            db.Entry(link).State = EntityState.Detached;
        }

        foreach (var source in sources)
        {
            db.Entry(source).State = EntityState.Detached;
        }

        await artworkDelete.DeleteIfUnusedAsync(
            sources.SelectMany(a => new[] { a.PhotoId, a.BackgroundId }).OfType<long>().Distinct().ToArray(),
            cancellationToken);
    }

    /// <param name="Kept">The album that remains, as an album of the target.</param>
    /// <param name="Sources">The albums of the merged artists with this name.</param>
    /// <param name="Merged">The ones among them that are merged into <paramref name="Kept"/>.</param>
    private record AlbumGroup(Album Kept, List<Album> Sources, List<Album> Merged);

    /// <param name="ArtistIds">The ids of the song's artists, in order.</param>
    private record AffectedSong(long Id, long AlbumId, List<long> ArtistIds);
}
