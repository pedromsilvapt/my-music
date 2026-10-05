using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Albums;

/// <summary>
/// Default implementation of <see cref="IAlbumEditService"/>.
/// </summary>
public class AlbumEditService(
    MusicDbContext db,
    IFileTransactionService fileTransactions,
    IAdvisoryLockService advisoryLocks,
    ISongUpdateService songUpdate,
    ILogger<AlbumEditService> logger) : IAlbumEditService
{
    private const int NameMaxLength = 256;

    /// <inheritdoc />
    public async Task EditAsync(long ownerId, IReadOnlyCollection<AlbumEditInput> edits,
        CancellationToken cancellationToken = default)
    {
        var inputs = edits.Select(edit => edit with { Name = edit.Name.Trim() }).ToList();

        foreach (var input in inputs)
        {
            if (input.Name.Length == 0)
            {
                throw new ValidationException("Album name cannot be empty");
            }

            if (input.Name.Length > NameMaxLength)
            {
                throw new ValidationException($"Album name cannot be longer than {NameMaxLength} characters");
            }
        }

        var ids = inputs.Select(input => input.AlbumId).Distinct().ToArray();
        if (ids.Length != inputs.Count)
        {
            throw new ValidationException("An album cannot be edited more than once in the same operation");
        }

        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);

        var albums = await db.Albums
            .Include(a => a.Artist)
            .Where(a => ids.Contains(a.Id) && a.OwnerId == ownerId)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        if (ids.Except(albums.Keys).Cast<long?>().FirstOrDefault() is { } missingId)
        {
            throw new AlbumNotFoundException(missingId);
        }

        var renames = inputs
            .Select(input => new Rename(albums[input.AlbumId], input.Name))
            .Where(rename => rename.Album.Name != rename.NewName)
            .ToList();

        if (renames.FirstOrDefault(rename => rename.Album.Name == Album.PlaceholderName) is { } placeholder)
        {
            throw new ValidationException(
                $"The album '{placeholder.Album.Name}' of '{placeholder.Album.Artist.Name}' cannot be renamed: it holds the songs that have no album");
        }

        // The albums being renamed, under their old and their new names
        locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db,
            AlbumArtistLockKeys.Create(ownerId, [],
                renames.SelectMany(rename => new[]
                {
                    (rename.Album.Artist.Name, rename.Album.Name), (rename.Album.Artist.Name, rename.NewName),
                })),
            cancellationToken);

        await EnsureNamesAreFreeAsync(ownerId, renames, cancellationToken);

        foreach (var input in inputs)
        {
            var album = albums[input.AlbumId];
            album.Name = input.Name;
            album.Year = input.Year;
        }

        await db.SaveChangesAsync(cancellationToken);

        var songIds = new List<long>();
        if (renames.Count > 0)
        {
            var renamedIds = renames.Select(rename => rename.Album.Id).ToArray();
            songIds = await AlbumArtistSongsQuery.OfAlbums(db, ownerId, renamedIds)
                .OrderBy(s => s.Id)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            // An empty update re-applies each song's rows, which now carry the new album name, to its file
            await songUpdate.UpdateSongsAsync(db, files,
                songIds.Select(songId => (songId, new SongUpdateModel())),
                new SongUpdateOptions { KeepAlbumIds = renamedIds }, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Edited {AlbumsCount} albums ({AlbumIds}) of user {UserId}, renaming {RenamedCount} and updating {SongsCount} songs",
            ids.Length, ids, ownerId, renames.Count, songIds.Count);
    }

    /// <summary>
    ///     Album names are unique per artist: a renamed album cannot take the name another album of its artist has,
    ///     nor the name another album of its artist is being renamed to. Such albums are merged, not renamed.
    /// </summary>
    private async Task EnsureNamesAreFreeAsync(long ownerId, List<Rename> renames,
        CancellationToken cancellationToken)
    {
        if (renames.Count == 0)
        {
            return;
        }

        var artistIds = renames.Select(rename => rename.Album.ArtistId).Distinct().ToArray();
        var newNames = renames.Select(rename => rename.NewName).Distinct().ToArray();
        var existing = await db.Albums
            .Where(a => a.OwnerId == ownerId && artistIds.Contains(a.ArtistId) && newNames.Contains(a.Name))
            .Select(a => new { a.Id, a.ArtistId, a.Name })
            .ToListAsync(cancellationToken);

        foreach (var rename in renames)
        {
            var album = rename.Album;

            if (existing.Any(other => other.Id != album.Id && other.ArtistId == album.ArtistId
                                                           && other.Name == rename.NewName)
                || renames.Any(other => other.Album.Id != album.Id && other.Album.ArtistId == album.ArtistId
                                                                   && other.NewName == rename.NewName))
            {
                throw new AlbumAlreadyExistsException(
                    $"Artist '{album.Artist.Name}' already has an album named '{rename.NewName}'. To join the two albums, merge them instead.");
            }
        }
    }

    private record Rename(Album Album, string NewName);
}
