using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Default implementation of <see cref="ISongTimestampsUpdateService"/>.
/// </summary>
public class SongTimestampsUpdateService(
    MusicDbContext db,
    ICurrentUser currentUser,
    IAdvisoryLockService advisoryLockService,
    ILogger<SongTimestampsUpdateService> logger) : ISongTimestampsUpdateService
{
    /// <inheritdoc />
    public async Task<Song> UpdateAsync(long songId, SongTimestampsUpdate timestamps,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Imports updating this song must not interleave with the update
        await using var songLock = await advisoryLockService.AcquireTransactionLocksAsync(db,
            [AdvisoryLockKey.Create(AdvisoryLockScope.Song, userId, songId.ToString())], cancellationToken);

        var song = await db.Songs
            .FirstOrDefaultAsync(s => s.Id == songId && s.OwnerId == userId, cancellationToken);

        if (song == null)
        {
            throw new InvalidOperationException("Song not found or access denied");
        }

        // The timestamps are stored as chosen by the user: ModifiedAt is deliberately not set to the current time
        song.CreatedAt = timestamps.CreatedAt.ToUniversalTime();
        song.ModifiedAt = timestamps.ModifiedAt.ToUniversalTime();
        song.AddedAt = timestamps.AddedAt?.ToUniversalTime();
        song.FileModifiedAt = timestamps.FileModifiedAt?.ToUniversalTime();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Song {SongId} timestamps updated", songId);

        return song;
    }
}
