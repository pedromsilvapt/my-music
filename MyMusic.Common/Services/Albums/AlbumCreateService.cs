using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Albums;

/// <summary>
/// Default implementation of <see cref="IAlbumCreateService"/>.
/// </summary>
public class AlbumCreateService(
    MusicDbContext db,
    IAdvisoryLockService advisoryLocks,
    ILogger<AlbumCreateService> logger) : IAlbumCreateService
{
    private const int NameMaxLength = 256;

    /// <inheritdoc />
    public async Task<Album> CreateAsync(long ownerId, AlbumCreateInput input,
        CancellationToken cancellationToken = default)
    {
        var name = input.Name.Trim();

        if (name.Length == 0)
        {
            throw new ValidationException("Album name cannot be empty");
        }

        if (name.Length > NameMaxLength)
        {
            throw new ValidationException($"Album name cannot be longer than {NameMaxLength} characters");
        }

        var artist = await db.Artists
                .FirstOrDefaultAsync(a => a.Id == input.ArtistId && a.OwnerId == ownerId, cancellationToken)
            ?? throw new ValidationException($"Artist not found with id {input.ArtistId}");

        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The keys a song import takes before finding-or-creating this same album
        locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db,
            AlbumArtistLockKeys.Create(ownerId, [], [(artist.Name, name)]), cancellationToken);

        if (await db.Albums.AnyAsync(a => a.OwnerId == ownerId && a.ArtistId == artist.Id && a.Name == name,
                cancellationToken))
        {
            throw new AlbumAlreadyExistsException($"Artist '{artist.Name}' already has an album named '{name}'");
        }

        var album = new Album
        {
            Name = name,
            Artist = artist,
            ArtistId = artist.Id,
            OwnerId = ownerId,
            Year = input.Year,
            CreatedAt = DateTime.UtcNow,
        };

        db.Albums.Add(album);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created album {AlbumName} with ID {AlbumId} for user {UserId}",
            album.Name, album.Id, ownerId);

        return album;
    }
}
