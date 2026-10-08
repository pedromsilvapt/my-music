using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Generates the paths of songs being added to a device.
/// </summary>
internal static class SongDeviceNewPaths
{
    /// <summary>
    /// Returns the paths taken on the device: the paths of its SongDevices, and the paths requested for them.
    /// </summary>
    public static async Task<HashSet<string>> LoadUsedPathsAsync(MusicDbContext db, long deviceId, CancellationToken cancellationToken)
    {
        var paths = await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId)
            .Select(sd => new { sd.DevicePath, sd.RequestedPath })
            .ToListAsync(cancellationToken);

        var usedPaths = paths.Select(p => p.DevicePath).ToHashSet();
        usedPaths.UnionWith(paths.Where(p => p.RequestedPath != null).Select(p => p.RequestedPath!));

        return usedPaths;
    }

    /// <summary>
    /// Returns the paths of the SongDevices marked for removal that a typed path can take: the next sync
    /// deletes their files before giving their paths to other files. A path already requested for
    /// another SongDevice is not one of them.
    /// </summary>
    public static async Task<HashSet<string>> LoadRemovedPathsAsync(MusicDbContext db, long deviceId, CancellationToken cancellationToken)
    {
        var removedPaths = await db.SongDevices
            .Where(sd => sd.DeviceId == deviceId && sd.SyncAction == SongSyncAction.Remove)
            .Select(sd => sd.DevicePath)
            .ToHashSetAsync(cancellationToken);

        if (removedPaths.Count > 0)
        {
            var requestedPaths = await db.SongDevices
                .Where(sd => sd.DeviceId == deviceId && sd.RequestedPath != null && sd.RequestedPath != sd.DevicePath)
                .Select(sd => sd.RequestedPath!)
                .ToListAsync(cancellationToken);
            removedPaths.ExceptWith(requestedPaths);
        }

        return removedPaths;
    }

    /// <summary>
    /// Generates the path of <paramref name="song"/> with <paramref name="namingStrategy"/>, makes it
    /// unique among <paramref name="usedPaths"/> and adds it to them.
    /// </summary>
    public static string Take(ISyncPathResolver pathResolver, TemplateNamingStrategy namingStrategy, Song song, HashSet<string> usedPaths)
    {
        var naming = new NamingMetadata { Extension = Path.GetExtension(song.RepositoryPath) };
        var basePath = namingStrategy.Generate(EntityConverter.ToSong(song), naming);
        var path = pathResolver.GetUniquePath(basePath, usedPaths);

        usedPaths.Add(path);

        return path;
    }
}
