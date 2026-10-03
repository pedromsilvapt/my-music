using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Default implementation of <see cref="ISongChecksumRecalculateService"/>.
/// </summary>
public class SongChecksumRecalculateService(
    MusicDbContext db,
    ICurrentUser currentUser,
    IFileSystem fileSystem,
    IAdvisoryLockService advisoryLockService,
    ILogger<SongChecksumRecalculateService> logger) : ISongChecksumRecalculateService
{
    /// <inheritdoc />
    public async Task<SongChecksumRecalculateResult> RecalculateAsync(long songId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Imports updating this song must not interleave with the recalculation
        await using var songLock = await advisoryLockService.AcquireTransactionLocksAsync(db,
            [AdvisoryLockKey.Create(AdvisoryLockScope.Song, userId, songId.ToString())], cancellationToken);

        var song = await db.Songs
            .FirstOrDefaultAsync(s => s.Id == songId && s.OwnerId == userId, cancellationToken);

        if (song == null)
        {
            throw new InvalidOperationException("Song not found or access denied");
        }

        if (!fileSystem.File.Exists(song.RepositoryPath))
        {
            throw new FileNotFoundException($"File of song {songId} not found at {song.RepositoryPath}");
        }

        var checksumAlgorithm = ChecksumService.CreateChecksumAlgorithm();
        var checksumAlgorithmName = checksumAlgorithm.GetType().Name;
        var repositoryPath = song.RepositoryPath;
        var checksum = await Task.Run(
            () => ChecksumService.CalculateChecksum(fileSystem, checksumAlgorithm, repositoryPath), cancellationToken);

        if (checksum == song.Checksum && checksumAlgorithmName == song.ChecksumAlgorithm)
        {
            return new SongChecksumRecalculateResult { Changed = false };
        }

        logger.LogInformation("Song {SongId} checksum recalculated: {PreviousChecksum} → {NewChecksum}",
            songId, song.Checksum, checksum);

        // Only the record was outdated: the file itself did not change, so FileModifiedAt and the song's devices stay
        song.Checksum = checksum;
        song.ChecksumAlgorithm = checksumAlgorithmName;
        song.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SongChecksumRecalculateResult { Changed = true };
    }
}
