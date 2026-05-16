using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyMusic.Common.Services;

public class SongMergeService(ILogger<SongMergeService> logger) : ISongMergeService
{
    public async Task<SongMergeResult> MergeSongsAsync(
        MusicDbContext db,
        long keepSongId,
        long mergeFromSongId,
        CancellationToken cancellationToken = default)
    {
        if (keepSongId == mergeFromSongId)
        {
            logger.LogDebug("Merge skipped: keepSongId and mergeFromSongId are the same ({SongId})", keepSongId);
            return SongMergeResult.Succeeded();
        }

        logger.LogInformation("Merging song {MergeFromSongId} into {KeepSongId}", mergeFromSongId, keepSongId);

        var keepSong = await db.Songs.FindAsync([keepSongId], cancellationToken);
        var mergeFromSong = await db.Songs.FindAsync([mergeFromSongId], cancellationToken);

        if (keepSong == null)
        {
            logger.LogDebug("Merge aborted: keepSong {KeepSongId} not found", keepSongId);
            return SongMergeResult.Failed($"Song with id {keepSongId} not found");
        }

        if (mergeFromSong == null)
        {
            logger.LogDebug("Merge aborted: mergeFromSong {MergeFromSongId} not found", mergeFromSongId);
            return SongMergeResult.Failed($"Song with id {mergeFromSongId} not found");
        }

        logger.LogDebug("Merging: KeepSong[{KeepId}] '{KeepTitle}' (Album='{KeepAlbum}') <- MergeFromSong[{MergeId}] '{MergeTitle}' (Album='{MergeAlbum}')",
            keepSongId, keepSong.Title, keepSong.Album?.Name ?? "(null)", mergeFromSongId, mergeFromSong.Title, mergeFromSong.Album?.Name ?? "(null)");

        var hasExistingTransaction = db.Database.CurrentTransaction != null;
        var transaction = hasExistingTransaction 
            ? null 
            : await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await MergeSongDevicesAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergePlaylistSongsAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergePlayHistoryAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergePurchasedSongsAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergeSongSourcesAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergeMetadataFetchTasksAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergeAutoFetchedMetadataAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergeAuditNonConformitiesAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergeDeviceSyncSessionRecordsAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergePlaylistCurrentSongIdAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergeExcludedDuplicatePairsAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergeSongArtistsAsync(db, keepSongId, mergeFromSongId, cancellationToken);
            await MergeSongGenresAsync(db, keepSongId, mergeFromSongId, cancellationToken);

            db.Songs.Remove(mergeFromSong);

            await db.SaveChangesAsync(cancellationToken);
            
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogInformation("Successfully merged song {MergeFromSongId} into {KeepSongId}", mergeFromSongId, keepSongId);

            return SongMergeResult.Succeeded();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to merge song {MergeFromSongId} into {KeepSongId}", mergeFromSongId, keepSongId);
            return SongMergeResult.Failed($"Merge failed: {ex.Message}");
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task MergeSongDevicesAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        var mergeFromDevices = await db.SongDevices
            .Where(sd => sd.SongId == mergeFromSongId)
            .ToListAsync(cancellationToken);

        var keepDevices = await db.SongDevices
            .Where(sd => sd.SongId == keepSongId)
            .ToListAsync(cancellationToken);

        var keepDeviceIds = keepDevices.Select(sd => sd.DeviceId).ToHashSet();
        var duplicateCount = mergeFromDevices.Count(d => keepDeviceIds.Contains(d.DeviceId));

        logger.LogDebug("  >> MergeSongDevices: {FromCount} from source, {KeepCount} in target, {DuplicateCount} duplicates to remove, {TransferCount} to transfer",
            mergeFromDevices.Count, keepDevices.Count, duplicateCount, mergeFromDevices.Count - duplicateCount);

        foreach (var device in mergeFromDevices)
        {
            if (keepDeviceIds.Contains(device.DeviceId))
            {
                db.SongDevices.Remove(device);
            }
            else
            {
                device.SongId = keepSongId;
                db.SongDevices.Update(device);
            }
        }
    }

    private async Task MergePlaylistSongsAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        var mergeFromPlaylistSongs = await db.PlaylistSongs
            .Where(ps => ps.SongId == mergeFromSongId)
            .ToListAsync(cancellationToken);

        var keepPlaylistSongs = await db.PlaylistSongs
            .Where(ps => ps.SongId == keepSongId)
            .ToListAsync(cancellationToken);

        var keepPlaylistIds = keepPlaylistSongs.Select(ps => ps.PlaylistId).ToHashSet();
        var duplicateCount = mergeFromPlaylistSongs.Count(ps => keepPlaylistIds.Contains(ps.PlaylistId));

        logger.LogDebug("  >> MergePlaylistSongs: {FromCount} from source, {KeepCount} in target, {DuplicateCount} duplicates to remove, {TransferCount} to transfer",
            mergeFromPlaylistSongs.Count, keepPlaylistSongs.Count, duplicateCount, mergeFromPlaylistSongs.Count - duplicateCount);

        foreach (var playlistSong in mergeFromPlaylistSongs)
        {
            if (keepPlaylistIds.Contains(playlistSong.PlaylistId))
            {
                db.PlaylistSongs.Remove(playlistSong);
            }
            else
            {
                playlistSong.SongId = keepSongId;
                db.PlaylistSongs.Update(playlistSong);
            }
        }
    }

    private async Task MergePlayHistoryAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        await db.PlayHistories
            .Where(ph => ph.SongId == mergeFromSongId)
            .ExecuteUpdateAsync(s => s.SetProperty(ph => ph.SongId, keepSongId), cancellationToken);
    }

    private async Task MergePurchasedSongsAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        await db.PurchasedSongs
            .Where(ps => ps.SongId == mergeFromSongId)
            .ExecuteUpdateAsync(s => s.SetProperty(ps => ps.SongId, keepSongId), cancellationToken);
    }

    private async Task MergeSongSourcesAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        var mergeFromSources = await db.SongSources
            .Where(ss => ss.SongId == mergeFromSongId)
            .ToListAsync(cancellationToken);

        var keepSources = await db.SongSources
            .Where(ss => ss.SongId == keepSongId)
            .ToListAsync(cancellationToken);

        var keepSourceKeys = keepSources.Select(ss => (ss.SourceId, ss.ExternalId)).ToHashSet();

        foreach (var source in mergeFromSources)
        {
            if (keepSourceKeys.Contains((source.SourceId, source.ExternalId)))
            {
                db.SongSources.Remove(source);
            }
            else
            {
                source.SongId = keepSongId;
                db.SongSources.Update(source);
            }
        }
    }

    private async Task MergeMetadataFetchTasksAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        await db.MetadataFetchTasks
            .Where(mft => mft.SongId == mergeFromSongId)
            .ExecuteUpdateAsync(s => s.SetProperty(mft => mft.SongId, keepSongId), cancellationToken);
    }

    private async Task MergeAutoFetchedMetadataAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        await db.AutoFetchedMetadata
            .Where(afm => afm.SongId == mergeFromSongId)
            .ExecuteUpdateAsync(s => s.SetProperty(afm => afm.SongId, keepSongId), cancellationToken);
    }

    private async Task MergeAuditNonConformitiesAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        await db.AuditNonConformities
            .Where(anc => anc.SongId == mergeFromSongId)
            .ExecuteUpdateAsync(s => s.SetProperty(anc => anc.SongId, keepSongId), cancellationToken);
    }

    private async Task MergeDeviceSyncSessionRecordsAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        await db.DeviceSyncSessionRecords
            .Where(dsr => dsr.SongId == mergeFromSongId)
            .ExecuteUpdateAsync(s => s.SetProperty(dsr => dsr.SongId, keepSongId), cancellationToken);
    }

    private async Task MergePlaylistCurrentSongIdAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        await db.Playlists
            .Where(p => p.CurrentSongId == mergeFromSongId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CurrentSongId, keepSongId), cancellationToken);
    }

    private async Task MergeExcludedDuplicatePairsAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        var pairsWithSongA = await db.ExcludedDuplicatePairs
            .Where(edp => edp.SongAId == mergeFromSongId)
            .ToListAsync(cancellationToken);

        var pairsWithSongB = await db.ExcludedDuplicatePairs
            .Where(edp => edp.SongBId == mergeFromSongId)
            .ToListAsync(cancellationToken);

        foreach (var pair in pairsWithSongA)
        {
            pair.SongAId = keepSongId;
            db.ExcludedDuplicatePairs.Update(pair);
        }

        foreach (var pair in pairsWithSongB)
        {
            if (pair.SongAId == keepSongId)
            {
                db.ExcludedDuplicatePairs.Remove(pair);
            }
            else
            {
                pair.SongBId = keepSongId;
                db.ExcludedDuplicatePairs.Update(pair);
            }
        }
    }

    private async Task MergeSongArtistsAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        var mergeFromArtists = await db.SongArtists
            .Where(sa => sa.SongId == mergeFromSongId)
            .ToListAsync(cancellationToken);

        var keepArtists = await db.SongArtists
            .Where(sa => sa.SongId == keepSongId)
            .ToListAsync(cancellationToken);

        var keepArtistIds = keepArtists.Select(sa => sa.ArtistId).ToHashSet();
        var duplicateCount = mergeFromArtists.Count(sa => keepArtistIds.Contains(sa.ArtistId));

        logger.LogDebug("  >> MergeSongArtists: {FromCount} from source, {KeepCount} in target, {DuplicateCount} duplicates to remove, {TransferCount} to transfer",
            mergeFromArtists.Count, keepArtists.Count, duplicateCount, mergeFromArtists.Count - duplicateCount);

        foreach (var artist in mergeFromArtists)
        {
            if (keepArtistIds.Contains(artist.ArtistId))
            {
                db.SongArtists.Remove(artist);
            }
            else
            {
                artist.SongId = keepSongId;
                db.SongArtists.Update(artist);
            }
        }
    }

    private async Task MergeSongGenresAsync(MusicDbContext db, long keepSongId, long mergeFromSongId, CancellationToken cancellationToken)
    {
        var mergeFromGenres = await db.SongGenres
            .Where(sg => sg.SongId == mergeFromSongId)
            .ToListAsync(cancellationToken);

        var keepGenres = await db.SongGenres
            .Where(sg => sg.SongId == keepSongId)
            .ToListAsync(cancellationToken);

        var keepGenreIds = keepGenres.Select(sg => sg.GenreId).ToHashSet();
        var duplicateCount = mergeFromGenres.Count(sg => keepGenreIds.Contains(sg.GenreId));

        logger.LogDebug("  >> MergeSongGenres: {FromCount} from source, {KeepCount} in target, {DuplicateCount} duplicates to remove, {TransferCount} to transfer",
            mergeFromGenres.Count, keepGenres.Count, duplicateCount, mergeFromGenres.Count - duplicateCount);

        foreach (var genre in mergeFromGenres)
        {
            if (keepGenreIds.Contains(genre.GenreId))
            {
                db.SongGenres.Remove(genre);
            }
            else
            {
                genre.SongId = keepSongId;
                db.SongGenres.Update(genre);
            }
        }
    }
}
