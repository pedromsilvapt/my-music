using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Albums;

namespace MyMusic.Common.Services.Artists;

/// <summary>
/// Default implementation of <see cref="IArtistEditService"/>.
/// </summary>
public class ArtistEditService(
    MusicDbContext db,
    IFileTransactionService fileTransactions,
    IAdvisoryLockService advisoryLocks,
    ISongUpdateService songUpdate,
    ILogger<ArtistEditService> logger) : IArtistEditService
{
    private const int NameMaxLength = 256;

    /// <inheritdoc />
    public async Task EditAsync(long ownerId, IReadOnlyCollection<ArtistEditInput> edits,
        CancellationToken cancellationToken = default)
    {
        var inputs = edits.Select(edit => edit with { Name = edit.Name.Trim() }).ToList();

        foreach (var input in inputs)
        {
            if (input.Name.Length == 0)
            {
                throw new ValidationException("Artist name cannot be empty");
            }

            if (input.Name.Length > NameMaxLength)
            {
                throw new ValidationException($"Artist name cannot be longer than {NameMaxLength} characters");
            }
        }

        var ids = inputs.Select(input => input.ArtistId).Distinct().ToArray();
        if (ids.Length != inputs.Count)
        {
            throw new ValidationException("An artist cannot be edited more than once in the same operation");
        }

        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);

        var artists = await db.Artists
            .Where(a => ids.Contains(a.Id) && a.OwnerId == ownerId)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        if (ids.Except(artists.Keys).Cast<long?>().FirstOrDefault() is { } missingId)
        {
            throw new ArtistNotFoundException(missingId);
        }

        // The name is all there is to edit: an artist keeping its name has nothing to save
        var renames = inputs
            .Select(input => new Rename(artists[input.ArtistId], artists[input.ArtistId].Name, input.Name))
            .Where(rename => rename.OldName != rename.NewName)
            .ToList();

        if (renames.FirstOrDefault(rename => rename.OldName == Artist.PlaceholderName) is { } placeholder)
        {
            throw new ValidationException(
                $"The artist '{placeholder.OldName}' cannot be renamed: it holds the songs that have no artist");
        }

        var renamedIds = renames.Select(rename => rename.Artist.Id).ToArray();
        var albums = await db.Albums
            .Where(a => a.OwnerId == ownerId && renamedIds.Contains(a.ArtistId))
            .Select(a => new { a.ArtistId, a.Name })
            .ToListAsync(cancellationToken);
        var renamesByArtistId = renames.ToDictionary(rename => rename.Artist.Id);

        // The artists being renamed and their albums, under their old and their new names
        locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db,
            AlbumArtistLockKeys.Create(ownerId,
                renames.SelectMany(rename => new[] { rename.OldName, rename.NewName }),
                albums.SelectMany(album => new[]
                {
                    (renamesByArtistId[album.ArtistId].OldName, album.Name),
                    (renamesByArtistId[album.ArtistId].NewName, album.Name),
                })),
            cancellationToken);

        foreach (var rename in renames)
        {
            rename.Artist.Name = rename.NewName;
        }

        await db.SaveChangesAsync(cancellationToken);

        var songIds = await AlbumArtistSongsQuery.OfArtists(db, ownerId, renamedIds)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        // An empty update re-applies each song's rows, which now carry the new artist names, to its file
        await songUpdate.UpdateSongsAsync(db, files,
            songIds.Select(songId => (songId, new SongUpdateModel())),
            new SongUpdateOptions { KeepArtistIds = renamedIds }, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Edited {ArtistsCount} artists ({ArtistIds}) of user {UserId}, renaming {RenamedCount} and updating {SongsCount} songs",
            ids.Length, ids, ownerId, renames.Count, songIds.Count);
    }

    private record Rename(Artist Artist, string OldName, string NewName);
}
