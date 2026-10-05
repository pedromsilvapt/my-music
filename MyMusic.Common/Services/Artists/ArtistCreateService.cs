using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Artists;

/// <summary>
/// Default implementation of <see cref="IArtistCreateService"/>.
/// </summary>
public class ArtistCreateService(
    MusicDbContext db,
    IAdvisoryLockService advisoryLocks,
    ILogger<ArtistCreateService> logger) : IArtistCreateService
{
    private const int NameMaxLength = 256;

    /// <inheritdoc />
    public async Task<Artist> CreateAsync(long ownerId, string name, CancellationToken cancellationToken = default)
    {
        name = name.Trim();

        if (name.Length == 0)
        {
            throw new ValidationException("Artist name cannot be empty");
        }

        if (name.Length > NameMaxLength)
        {
            throw new ValidationException($"Artist name cannot be longer than {NameMaxLength} characters");
        }

        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The key a song import takes before finding-or-creating an artist with this name
        locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db,
            AlbumArtistLockKeys.Create(ownerId, [name], []), cancellationToken);

        var artist = new Artist
        {
            Name = name,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
        };

        db.Artists.Add(artist);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created artist {ArtistName} with ID {ArtistId} for user {UserId}",
            artist.Name, artist.Id, ownerId);

        return artist;
    }
}
