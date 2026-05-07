using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.AuditRules;

namespace MyMusic.Common.Services;

public class SoundalikeResolutionService(
    ISoundalikeMergeService mergeService,
    IOptions<Config> config,
    ILogger<SoundalikeResolutionService> logger) : ISoundalikeResolutionService
{
    public async Task<int> ResolveAsync(MusicDbContext db, long ownerId, List<GroupResolutionInput> resolutions, CancellationToken cancellationToken = default)
    {
        var resolvedCount = 0;

        foreach (var resolution in resolutions)
        {
            var allSongIds = new List<long> { resolution.PrimarySongId }
                .Concat(resolution.SecondaryActions.Select(a => a.SongId))
                .ToList();

            var songs = await db.Songs
                .Where(s => allSongIds.Contains(s.Id))
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

            if (mergeActions.Count > 0)
            {
                var mergeSongs = songs
                    .Where(s => mergeActions.Any(a => a.SongId == s.Id))
                    .ToList();
                await mergeService.MergeMetadataAsync(db, primarySong, mergeSongs, cancellationToken);
            }

            var secondaryIds = deleteActions.Select(a => a.SongId).ToHashSet();

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
                var basePath = namingStrategy.Generate(EntityConverter.ToSong(primarySong));

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
                    AddedAt = DateTime.UtcNow,
                });

                primaryDeviceIds.Add(deviceId);
            }

            foreach (var sd in secondarySongDevices)
            {
                sd.SongId = null;
                sd.SyncAction = SongSyncAction.Remove;
                db.Update(sd);
            }

            var secondaries = songs.Where(s => secondaryIds.Contains(s.Id)).ToList();
            db.Songs.RemoveRange(secondaries);

            var nonConformity = await db.AuditNonConformities
                .FirstOrDefaultAsync(nc => nc.Id == resolution.NonConformityId && nc.OwnerId == ownerId,
                    cancellationToken);
            if (nonConformity != null)
            {
                db.AuditNonConformities.Remove(nonConformity);
            }

            resolvedCount++;
        }

        await db.SaveChangesAsync(cancellationToken);

        return resolvedCount;
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
