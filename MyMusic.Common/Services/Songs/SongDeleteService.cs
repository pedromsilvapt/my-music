using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;

namespace MyMusic.Common.Services;

public class SongDeleteService(
    MusicDbContext db,
    ICurrentUser currentUser,
    IFileSystem fileSystem,
    IOptions<Config> config,
    IAlbumDeleteService albumDeleteService,
    IArtistDeleteService artistDeleteService,
    IGenreDeleteService genreDeleteService,
    IArtworkDeleteService artworkDeleteService,
    ILogger<SongDeleteService> logger) : ISongDeleteService
{
    public async Task<int> DeleteAsync(long[] songIds, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;

        var songs = await db.Songs
            .Where(s => songIds.Contains(s.Id) && s.OwnerId == userId)
            .ToListAsync(cancellationToken);

        if (songs.Count != songIds.Length)
        {
            throw new InvalidOperationException("One or more songs not found or access denied");
        }

        var albumIds = songs.Select(s => s.AlbumId).Distinct().ToArray();
        var artistIds = await db.SongArtists
            .Where(sa => songIds.Contains(sa.SongId))
            .Select(sa => sa.ArtistId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var genreIds = await db.SongGenres
            .Where(sg => songIds.Contains(sg.SongId))
            .Select(sg => sg.GenreId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var songArtworkIds = songs
            .Where(s => s.CoverId.HasValue)
            .Select(s => s.CoverId!.Value)
            .Distinct()
            .ToArray();

        var albumArtworkIds = await db.Albums
            .Where(a => albumIds.Contains(a.Id) && a.CoverId != null)
            .Select(a => a.CoverId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var artistArtworkData = await db.Artists
            .Where(a => artistIds.Contains(a.Id) && (a.PhotoId != null || a.BackgroundId != null))
            .Select(a => new { a.PhotoId, a.BackgroundId })
            .ToListAsync(cancellationToken);
        var artistArtworkIds = artistArtworkData
            .SelectMany(a => new long?[] { a.PhotoId, a.BackgroundId })
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var repositoryPaths = songs.Select(s => s.RepositoryPath).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await NullSongDevicesAndMarkForRemovalAsync(songIds, cancellationToken);

        await DeleteSongArtistsAsync(songIds, cancellationToken);
        await DeleteSongGenresAsync(songIds, cancellationToken);
        await DeleteSongSourcesAsync(songIds, cancellationToken);
        await DeletePlaylistSongsAsync(songIds, cancellationToken);
        await DeleteAutoFetchedMetadataAsync(songIds, cancellationToken);
        await DeleteMetadataFetchTasksAsync(songIds, cancellationToken);
        await DeleteExcludedDuplicatePairsAsync(songIds, cancellationToken);
        await DeleteSongAcousticFingerprintsAsync(songs, cancellationToken);
        await DeleteAuditNonConformitiesAsync(songIds, cancellationToken);
        await DeletePlayHistoriesAsync(songIds, cancellationToken);
        await DeletePurchasedSongsAsync(songIds, cancellationToken);

        var deletedCount = await db.Songs
            .Where(s => songIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);
        logger.LogDebug("Deleted {Count} Songs", deletedCount);

        await albumDeleteService.DeleteIfUnusedAsync(albumIds, cancellationToken);
        await artistDeleteService.DeleteIfUnusedAsync(artistIds, cancellationToken);
        await genreDeleteService.DeleteIfUnusedAsync(genreIds, cancellationToken);

        await artworkDeleteService.DeleteIfUnusedAsync(
            songArtworkIds.Concat(albumArtworkIds).Concat(artistArtworkIds).Distinct().ToArray(),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        foreach (var repositoryPath in repositoryPaths)
        {
            if (fileSystem.File.Exists(repositoryPath))
            {
                logger.LogDebug("Deleting song file: {Path}", repositoryPath);
                fileSystem.File.Delete(repositoryPath);
            }
            else
            {
                logger.LogWarning("Song file not found on disk, skipping deletion: {Path}", repositoryPath);
            }
        }

        logger.LogInformation("Deleted {Count} songs", deletedCount);
        return deletedCount;
    }

    private async Task NullSongDevicesAndMarkForRemovalAsync(long[] songIds, CancellationToken ct)
    {
        var songDevices = await db.SongDevices
            .Where(sd => sd.SongId != null && songIds.Contains(sd.SongId.Value))
            .ToListAsync(ct);

        foreach (var sd in songDevices)
        {
            if (sd.SyncAction == SongSyncAction.Download)
            {
                db.SongDevices.Remove(sd);
            }
            else
            {
                sd.SongId = null;
                sd.SyncAction = SongSyncAction.Remove;
                sd.SyncActionReason = "Song deleted from library";
            }
        }

        var updatedCount = songDevices.Count;
        if (updatedCount > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogDebug("Processed {Count} SongDevices (nulled SongId, marked for removal or hard-deleted)", updatedCount);
        }
    }

    private async Task DeleteSongArtistsAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.SongArtists
            .Where(sa => songIds.Contains(sa.SongId))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} SongArtists", count);
    }

    private async Task DeleteSongGenresAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.SongGenres
            .Where(sg => songIds.Contains(sg.SongId))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} SongGenres", count);
    }

    private async Task DeleteSongSourcesAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.SongSources
            .Where(ss => songIds.Contains(ss.SongId))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} SongSources", count);
    }

    private async Task DeletePlaylistSongsAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.PlaylistSongs
            .Where(ps => songIds.Contains(ps.SongId))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} PlaylistSongs", count);
    }

    private async Task DeleteAutoFetchedMetadataAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.AutoFetchedMetadata
            .Where(m => songIds.Contains(m.SongId))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} AutoFetchedMetadata", count);
    }

    private async Task DeleteMetadataFetchTasksAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.MetadataFetchTasks
            .Where(t => songIds.Contains(t.SongId))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} MetadataFetchTasks", count);
    }

    private async Task DeleteExcludedDuplicatePairsAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.ExcludedDuplicatePairs
            .Where(p => songIds.Contains(p.SongAId) || songIds.Contains(p.SongBId))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} ExcludedDuplicatePairs", count);
    }

    private async Task DeleteSongAcousticFingerprintsAsync(List<Song> songs, CancellationToken ct)
    {
        var count = 0;
        foreach (var song in songs)
        {
            var deleted = await db.SongAcousticFingerprints
                .Where(f => f.Checksum == song.Checksum && f.ChecksumAlgorithm == song.ChecksumAlgorithm && f.OwnerId == song.OwnerId)
                .ExecuteDeleteAsync(ct);
            count += deleted;
        }
        logger.LogDebug("Deleted {Count} SongAcousticFingerprints", count);
    }

    private async Task DeleteAuditNonConformitiesAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.AuditNonConformities
            .Where(a => a.SongId != null && songIds.Contains(a.SongId.Value))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} AuditNonConformities", count);
    }

    private async Task DeletePlayHistoriesAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.PlayHistories
            .Where(h => songIds.Contains(h.SongId))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} PlayHistories", count);
    }

    private async Task DeletePurchasedSongsAsync(long[] songIds, CancellationToken ct)
    {
        var count = await db.PurchasedSongs
            .Where(p => p.SongId != null && songIds.Contains(p.SongId.Value))
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} PurchasedSongs", count);
    }
}
