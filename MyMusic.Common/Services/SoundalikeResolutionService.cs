using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.AuditRules;
using MyMusic.Common.Services.Songs;

namespace MyMusic.Common.Services;

public class SoundalikeResolutionService(
    ISoundalikeMergeService mergeService,
    ISongFileUpdateService songFileUpdate,
    IFileTransactionService fileTransactions,
    IOptions<Config> config,
    ILogger<SoundalikeResolutionService> logger) : ISoundalikeResolutionService
{
    public async Task<int> ResolveAsync(MusicDbContext db, long ownerId, List<GroupResolutionInput> resolutions, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);

        var resolvedCount = 0;
        var movedFiles = new List<(string PreviousPath, Song Song)>();

        foreach (var resolution in resolutions)
        {
            var allSongIds = new List<long> { resolution.PrimarySongId }
                .Concat(resolution.SecondaryActions.Select(a => a.SongId))
                .ToList();

            var songs = await db.Songs
                .Where(s => allSongIds.Contains(s.Id))
                .Include(s => s.Owner)
                .Include(s => s.Album).ThenInclude(a => a.Artist)
                .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
                .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Cover)
                .ToListAsync(cancellationToken);

            var primarySong = songs.FirstOrDefault(s => s.Id == resolution.PrimarySongId);
            if (primarySong == null)
                continue;

            if (primarySong.OwnerId != ownerId)
                throw new UnauthorizedAccessException($"User {ownerId} does not own song {primarySong.Id}");

            var mergeActions = resolution.SecondaryActions
                .Where(a => a.Action == SecondaryAction.Merge)
                .ToList();
            var deleteActions = resolution.SecondaryActions
                .Where(a => a.Action == SecondaryAction.Delete || a.Action == SecondaryAction.Merge)
                .ToList();

            foreach (var action in resolution.SecondaryActions)
            {
                var song = songs.FirstOrDefault(s => s.Id == action.SongId);
                if (song == null) continue;
                if (song.OwnerId != ownerId)
                    throw new UnauthorizedAccessException($"User {ownerId} does not own song {song.Id}");
            }

            foreach (var action in deleteActions)
            {
                if (songs.All(s => s.Id != action.SongId)) continue;

                db.SongMerges.Add(new SongMerge
                {
                    KeptSongId = primarySong.Id,
                    MergedSongId = action.SongId,
                    OwnerId = ownerId,
                    Kind = action.Action == SecondaryAction.Merge
                        ? SongMergeKind.SoundalikeMerge
                        : SongMergeKind.SoundalikeDelete,
                    MergedAt = DateTime.UtcNow,
                });
            }

            await ExcludeIgnoredSongsAsync(db, ownerId, primarySong, resolution, songs, cancellationToken);

            if (mergeActions.Count > 0)
            {
                var mergeSongs = songs
                    .Where(s => mergeActions.Any(a => a.SongId == s.Id))
                    .ToList();
                await mergeService.MergeMetadataAsync(db, primarySong, mergeSongs, cancellationToken);
            }

            var secondaryIds = deleteActions.Select(a => a.SongId).ToHashSet();

            KeepOldestDates(primarySong, songs.Where(s => secondaryIds.Contains(s.Id)).ToList());

            var primaryPlaylistIds = await db.PlaylistSongs
                .Where(ps => ps.SongId == primarySong.Id)
                .Select(ps => ps.PlaylistId)
                .ToHashSetAsync(cancellationToken);

            var secondaryPlaylistSongs = await db.PlaylistSongs
                .Where(ps => secondaryIds.Contains(ps.SongId))
                .ToListAsync(cancellationToken);

            var playlistsNeedingRedirect = await db.Playlists
                .Where(p => secondaryIds.Contains(p.CurrentSongId ?? -1))
                .ToListAsync(cancellationToken);

            foreach (var playlist in playlistsNeedingRedirect)
            {
                playlist.CurrentSongId = primarySong.Id;
                db.Update(playlist);
            }

            var playlistsToAddPrimary = secondaryPlaylistSongs
                .Where(ps => !primaryPlaylistIds.Contains(ps.PlaylistId))
                .GroupBy(ps => ps.PlaylistId)
                .ToList();

            foreach (var group in playlistsToAddPrimary)
            {
                var lowestOrder = group.Min(ps => ps.Order);
                db.PlaylistSongs.Add(new PlaylistSong
                {
                    PlaylistId = group.Key,
                    SongId = primarySong.Id,
                    Order = lowestOrder,
                    AddedAt = DateTime.UtcNow,
                });
            }

            db.PlaylistSongs.RemoveRange(secondaryPlaylistSongs);

            var primaryDeviceIds = await db.SongDevices
                .Where(sd => sd.SongId == primarySong.Id)
                .Select(sd => sd.DeviceId)
                .ToHashSetAsync(cancellationToken);

            var secondarySongDevices = await db.SongDevices
                .Where(sd => secondaryIds.Contains(sd.SongId ?? -1))
                .ToListAsync(cancellationToken);

            var devicesNeedingPrimary = secondarySongDevices
                .Where(sd => !primaryDeviceIds.Contains(sd.DeviceId))
                .GroupBy(sd => sd.DeviceId)
                .ToList();

            var devices = await db.Devices
                .Where(d => devicesNeedingPrimary.Select(g => g.Key).Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, cancellationToken);

            foreach (var group in devicesNeedingPrimary)
            {
                var deviceId = group.Key;
                if (!devices.TryGetValue(deviceId, out var device))
                    continue;

                var namingStrategy = new TemplateNamingStrategy(
                    device.NamingTemplate ?? config.Value.DefaultNamingTemplate);
                var secondaryPath = group.First().DevicePath;
                var naming = NamingMetadata.FromPath(secondaryPath);
                var basePath = namingStrategy.Generate(EntityConverter.ToSong(primarySong), naming);

                var existingPaths = await db.SongDevices
                    .Where(sd => sd.DeviceId == deviceId)
                    .Select(sd => sd.DevicePath)
                    .ToHashSetAsync(cancellationToken);
                var devicePath = GetUniquePath(basePath, existingPaths);

                db.SongDevices.Add(new SongDevice
                {
                    SongId = primarySong.Id,
                    DeviceId = deviceId,
                    DevicePath = devicePath,
                    SyncAction = SongSyncAction.Download,
                    SyncActionReason = "Soundalike resolution: replacing duplicate",
                    AddedAt = DateTime.UtcNow,
                });

                primaryDeviceIds.Add(deviceId);
            }

            foreach (var sd in secondarySongDevices)
            {
                sd.SongId = null;
                sd.SyncAction = SongSyncAction.Remove;
                sd.SyncActionReason = "Soundalike resolution: replacing duplicate";
                db.Update(sd);
            }

            // A purchase of a removed song is now a purchase of the kept song
            var secondaryPurchases = await db.PurchasedSongs
                .Where(ps => secondaryIds.Contains(ps.SongId ?? -1))
                .ToListAsync(cancellationToken);

            foreach (var purchase in secondaryPurchases)
            {
                purchase.SongId = primarySong.Id;
            }

            // What other audit rules found on the removed songs goes away with them
            var secondaryNonConformities = await db.AuditNonConformities
                .Where(nc => secondaryIds.Contains(nc.SongId ?? -1))
                .ToListAsync(cancellationToken);
            db.AuditNonConformities.RemoveRange(secondaryNonConformities);

            var secondaries = songs.Where(s => secondaryIds.Contains(s.Id)).ToList();
            db.Songs.RemoveRange(secondaries);

            if (resolution.NonConformityId is { } nonConformityId)
            {
                var nonConformity = await db.AuditNonConformities
                    .FirstOrDefaultAsync(nc => nc.Id == nonConformityId && nc.OwnerId == ownerId,
                        cancellationToken);
                if (nonConformity != null)
                {
                    db.AuditNonConformities.Remove(nonConformity);
                }
            }

            if (mergeActions.Count > 0)
            {
                // The merged songs must be gone before the file is written, so their paths are free for the kept song
                await db.SaveChangesAsync(cancellationToken);

                var fileUpdate = await songFileUpdate.UpdateAsync(db, files, primarySong,
                    () => "Soundalike resolution: merged metadata", cancellationToken);
                if (fileUpdate.PreviousPath is not null)
                {
                    movedFiles.Add((fileUpdate.PreviousPath, primarySong));
                }
            }

            resolvedCount++;
        }

        await db.SaveChangesAsync(cancellationToken);

        // The files only move once their new paths are saved, and move back if the commit fails
        foreach (var (previousPath, song) in movedFiles)
        {
            await files.MoveAsync(previousPath, song.RepositoryPath, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return resolvedCount;
    }

    /// <summary>
    /// Records each ignored song as not being a duplicate of the kept song, so the pair is not detected again.
    /// </summary>
    private static async Task ExcludeIgnoredSongsAsync(MusicDbContext db, long ownerId, Song primarySong,
        GroupResolutionInput resolution, List<Song> songs, CancellationToken cancellationToken)
    {
        var ignoredIds = resolution.SecondaryActions
            .Where(a => a.Action == SecondaryAction.Ignore && songs.Any(s => s.Id == a.SongId))
            .Select(a => a.SongId)
            .Distinct();

        foreach (var ignoredId in ignoredIds)
        {
            var (aId, bId) = primarySong.Id < ignoredId ? (primarySong.Id, ignoredId) : (ignoredId, primarySong.Id);

            var alreadyExcluded = await db.ExcludedDuplicatePairs
                .AnyAsync(p => p.SongAId == aId && p.SongBId == bId && p.OwnerId == ownerId, cancellationToken);
            if (alreadyExcluded) continue;

            db.ExcludedDuplicatePairs.Add(new ExcludedDuplicatePair
            {
                SongAId = aId,
                SongBId = bId,
                OwnerId = ownerId,
                Reason = "Soundalike resolution: ignored",
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    /// <summary>
    /// Makes the kept song as old as the oldest of the songs it absorbs, and marks it as modified now.
    /// </summary>
    private static void KeepOldestDates(Song primarySong, List<Song> absorbedSongs)
    {
        if (absorbedSongs.Count == 0) return;

        primarySong.CreatedAt = absorbedSongs
            .Select(s => s.CreatedAt)
            .Append(primarySong.CreatedAt)
            .Min()
            .ToUniversalTime();

        primarySong.AddedAt = absorbedSongs
            .Select(s => s.AddedAt)
            .Append(primarySong.AddedAt)
            .Min()
            ?.ToUniversalTime();

        primarySong.ModifiedAt = DateTime.UtcNow;
    }

    private static string GetUniquePath(string basePath, HashSet<string> existingPaths)
    {
        if (!existingPaths.Contains(basePath))
        {
            return basePath;
        }

        var directory = Path.GetDirectoryName(basePath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        var counter = 2;
        string newPath;
        do
        {
            newPath = Path.Combine(directory, $"{fileNameWithoutExt} ({counter}){extension}");
            counter++;
        } while (existingPaths.Contains(newPath));

        return newPath;
    }
}
