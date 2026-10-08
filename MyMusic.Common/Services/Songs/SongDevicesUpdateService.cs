using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using MyMusic.Common.Entities;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Sync;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Default implementation of <see cref="ISongDevicesUpdateService"/>.
/// </summary>
public class SongDevicesUpdateService(
    MusicDbContext db,
    ISyncPathResolver pathResolver,
    IOptions<Config> config) : ISongDevicesUpdateService
{
    private const int MaxPathLength = 1024;

    /// <inheritdoc />
    public async Task UpdateAsync(long ownerId, SongDevicesUpdateInput input, CancellationToken cancellationToken)
    {
        var songs = await db.Songs
            .Where(s => input.SongIds.Contains(s.Id) && s.OwnerId == ownerId)
            .Include(s => s.Album)
            .ThenInclude(a => a!.Artist)
            .Include(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Genres)
            .ThenInclude(g => g.Genre)
            .ToListAsync(cancellationToken);

        if (songs.Count == 0)
        {
            throw new Exception("No songs found");
        }

        var songIds = songs.Select(s => s.Id).ToList();
        var copyChanges = await LoadCopyChangesAsync(ownerId, input.Copies, songIds, cancellationToken);

        var deviceIds = input.Updates.Select(u => u.DeviceId)
            .Concat(input.Paths.Select(p => p.DeviceId))
            .Concat(copyChanges.Values.Select(c => c.DeviceId))
            .Distinct()
            .ToList();
        var devices = await db.Devices
            .Where(d => deviceIds.Contains(d.Id) && d.OwnerId == ownerId)
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var typedPaths = GroupTypedPaths(input.Paths, songs, devices);
        var memberships = input.Updates
            .GroupBy(u => u.DeviceId)
            .ToDictionary(g => g.Key, g => (bool?)g.Last().Include);

        var copyIncludes = copyChanges.ToDictionary(c => c.Key, c => c.Value.Include);

        foreach (var deviceId in deviceIds)
        {
            if (!devices.TryGetValue(deviceId, out var device))
            {
                continue;
            }

            await UpdateDeviceAsync(device, songs, memberships.GetValueOrDefault(deviceId),
                typedPaths.GetValueOrDefault(deviceId) ?? new TypedPaths(),
                copyIncludes, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The paths typed for one device: the ones of a copy, by SongDevice id, and the ones of a song
    /// (being added, or with a single copy), by song id.
    /// </summary>
    private sealed class TypedPaths
    {
        public Dictionary<long, (long SongId, string Path)> ByCopy { get; } = [];
        public Dictionary<long, string> BySong { get; } = [];

        public bool HasPathOf(long songId) => BySong.ContainsKey(songId) || ByCopy.Values.Any(p => p.SongId == songId);
    }

    /// <summary>
    /// Returns the copies to remove or restore one by one, with the device each one is on, rejecting the
    /// ones that are not copies of the songs on a device of <paramref name="ownerId"/>.
    /// </summary>
    private async Task<Dictionary<long, (long DeviceId, bool Include)>> LoadCopyChangesAsync(
        long ownerId, IReadOnlyList<SongDeviceCopyInput> copies, List<long> songIds, CancellationToken cancellationToken)
    {
        if (copies.Count == 0)
        {
            return [];
        }

        var copyIds = copies.Select(c => c.SongDeviceId).ToList();
        var copyDevices = await db.SongDevices
            .Where(sd => copyIds.Contains(sd.Id) && sd.SongId != null && songIds.Contains(sd.SongId.Value)
                         && sd.Device.OwnerId == ownerId)
            .ToDictionaryAsync(sd => sd.Id, sd => sd.DeviceId, cancellationToken);

        var changes = new Dictionary<long, (long DeviceId, bool Include)>();

        foreach (var copy in copies)
        {
            if (!copyDevices.TryGetValue(copy.SongDeviceId, out var deviceId))
            {
                throw new ValidationException($"Copy {copy.SongDeviceId} is not a copy of the songs being updated");
            }

            changes[copy.SongDeviceId] = (deviceId, copy.Include);
        }

        return changes;
    }

    /// <summary>
    /// Applies to one device the membership change (<paramref name="include"/>, null when the device has
    /// none), the copies removed or restored one by one, and the typed paths.
    /// </summary>
    private async Task UpdateDeviceAsync(
        Device device,
        List<Song> songs,
        bool? include,
        TypedPaths typedPaths,
        Dictionary<long, bool> copyChanges,
        CancellationToken cancellationToken)
    {
        var songIds = songs.Select(s => s.Id).ToList();

        // A song can be more than once on a device, at different paths
        var existingCopies = (await db.SongDevices
                .Where(sd => sd.SongId != null && songIds.Contains(sd.SongId.Value) && sd.DeviceId == device.Id)
                .OrderBy(sd => sd.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(sd => sd.SongId!.Value);

        var unknownCopyId = typedPaths.ByCopy
            .Where(p => !existingCopies[p.Value.SongId].Any(sd => sd.Id == p.Key))
            .Select(p => (long?)p.Key)
            .FirstOrDefault();
        if (unknownCopyId != null)
        {
            throw new ValidationException($"Copy {unknownCopyId} is not a copy of its song on device '{device.Name}'");
        }

        var usedPaths = await SongDeviceNewPaths.LoadUsedPathsAsync(db, device.Id, cancellationToken);

        // The files of the copies marked for removal are deleted by the next sync, so their paths can be typed
        // for other songs. They stay in usedPaths: the generated paths keep avoiding them
        var removedPaths = await SongDeviceNewPaths.LoadRemovedPathsAsync(db, device.Id, cancellationToken);

        // The typed paths are taken first, so the generated paths of the other songs are made unique around them
        var songsInOrder = songs.OrderByDescending(s => typedPaths.HasPathOf(s.Id)).ToList();

        TemplateNamingStrategy? namingStrategy = null;

        foreach (var song in songsInOrder)
        {
            var copies = existingCopies[song.Id].ToList();
            var songPath = typedPaths.BySong.GetValueOrDefault(song.Id);

            if (copies.Count == 0)
            {
                if (include == true)
                {
                    // A removed file still holds its path until the next sync, which downloads the song to
                    // the requested path: until then the song is at a generated one
                    var typedRemovedPath = songPath != null && removedPaths.Contains(songPath);
                    var requestedPath = songPath != null
                        ? TakeTypedPath(songPath, song, device, usedPaths, removedPaths)
                        : null;

                    var devicePath = requestedPath;
                    if (devicePath == null || typedRemovedPath)
                    {
                        namingStrategy ??= new TemplateNamingStrategy(
                            device.NamingTemplate ?? config.Value.DefaultNamingTemplate);
                        devicePath = SongDeviceNewPaths.Take(pathResolver, namingStrategy, song, usedPaths);
                    }

                    db.SongDevices.Add(new SongDevice
                    {
                        SongId = song.Id,
                        DeviceId = device.Id,
                        DevicePath = devicePath,
                        RequestedPath = requestedPath,
                        SyncAction = SongSyncAction.Download,
                        SyncActionReason = "Song included on device",
                        AddedAt = DateTime.UtcNow,
                    });
                }
                else if (songPath != null)
                {
                    throw new ValidationException(
                        $"Cannot set the path of song '{song.Title}' on device '{device.Name}': the song is not on it");
                }

                continue;
            }

            if (songPath != null && (copies.Count > 1 || typedPaths.ByCopy.ContainsKey(copies[0].Id)))
            {
                throw new ValidationException(copies.Count > 1
                    ? $"Cannot set the path of song '{song.Title}' on device '{device.Name}': the song is more than once on it"
                    : $"Song {song.Id} has more than one path for device {device.Id}");
            }

            // Adding a song that is on the device only brings its copies back when none of them is staying
            var restoreAll = include == true && copies.All(sd => sd.SyncAction == SongSyncAction.Remove);

            foreach (var copy in copies)
            {
                var copyChange = copyChanges.TryGetValue(copy.Id, out var includeCopy) ? includeCopy : (bool?)null;
                var typedPath = typedPaths.ByCopy.TryGetValue(copy.Id, out var copyPath) ? copyPath.Path : songPath;

                if (include == false && copyChange == true)
                {
                    throw new ValidationException(
                        $"Cannot restore a copy of song '{song.Title}' on device '{device.Name}': the song is being removed from it");
                }

                if (include == false || copyChange == false)
                {
                    if (typedPath != null)
                    {
                        throw new ValidationException(
                            $"Cannot set the path of song '{song.Title}' on device '{device.Name}': the song is being removed from it");
                    }

                    if (copy is { SyncAction: SongSyncAction.Download, LastSyncedModifiedAt: null })
                    {
                        db.SongDevices.Remove(copy);
                    }
                    else
                    {
                        copy.SyncAction = SongSyncAction.Remove;
                        copy.SyncActionReason = "Song excluded from device";
                    }

                    continue;
                }

                if (copy.SyncAction == SongSyncAction.Remove && (copyChange == true || restoreAll))
                {
                    copy.SyncAction = null;
                    copy.SyncActionReason = null;

                    // The path of the copy may have been typed for another song while it was marked for removal
                    if (!removedPaths.Remove(copy.DevicePath))
                    {
                        if (typedPath == copy.DevicePath)
                        {
                            throw new ValidationException(
                                $"Path '{typedPath}' is already used by another song on device '{device.Name}'");
                        }

                        // The next sync renames the file, unless a path was typed for it
                        if (typedPath == null)
                        {
                            copy.RequestedPath = pathResolver.GetUniquePath(copy.DevicePath, usedPaths);
                            usedPaths.Add(copy.RequestedPath);
                        }
                    }
                }

                if (typedPath != null)
                {
                    if (copy.SyncAction == SongSyncAction.Remove)
                    {
                        throw new ValidationException(
                            $"Cannot set the path of song '{song.Title}' on device '{device.Name}': the song is not on it");
                    }

                    ApplyTypedPath(copy, typedPath, song, device, usedPaths, removedPaths);
                }
            }
        }
    }

    /// <summary>
    /// Sets the typed path of a song that is on the device. A file the device already holds keeps its
    /// <see cref="SongDevice.DevicePath"/> (where the device reports it) until the next sync renames it.
    /// </summary>
    private static void ApplyTypedPath(
        SongDevice songDevice, string typedPath, Song song, Device device, HashSet<string> usedPaths, HashSet<string> removedPaths)
    {
        if (typedPath == (songDevice.RequestedPath ?? songDevice.DevicePath))
        {
            return;
        }

        if (songDevice.RequestedPath != null && songDevice.RequestedPath != songDevice.DevicePath)
        {
            usedPaths.Remove(songDevice.RequestedPath);
        }

        // Typing the current path back cancels the pending rename
        if (typedPath == songDevice.DevicePath)
        {
            songDevice.RequestedPath = null;
            return;
        }

        // A removed file still holds its path until the next sync, so the copy cannot be moved to it yet
        var movesNow = songDevice is { SyncAction: SongSyncAction.Download, LastSyncedModifiedAt: null }
                       && !removedPaths.Contains(typedPath);
        if (movesNow)
        {
            usedPaths.Remove(songDevice.DevicePath);
        }

        var path = TakeTypedPath(typedPath, song, device, usedPaths, removedPaths);

        songDevice.RequestedPath = path;
        if (movesNow)
        {
            songDevice.DevicePath = path;
        }
    }

    /// <summary>
    /// Validates a typed path and adds it to <paramref name="usedPaths"/>. A path in
    /// <paramref name="removedPaths"/> is taken from them instead: it can be typed once.
    /// </summary>
    private static string TakeTypedPath(
        string typedPath, Song song, Device device, HashSet<string> usedPaths, HashSet<string> removedPaths)
    {
        if (typedPath.Length > MaxPathLength)
        {
            throw new ValidationException($"Path '{typedPath}' is longer than {MaxPathLength} characters");
        }

        if (typedPath.Split('/').Any(segment => segment.Length == 0 || FilenameUtils.SanitizeFilename(segment) != segment))
        {
            throw new ValidationException(
                $"Path '{typedPath}' is not valid: it must be relative to the device's music folder, with folders separated by '/'");
        }

        var extension = Path.GetExtension(song.RepositoryPath);
        if (!string.Equals(Path.GetExtension(typedPath), extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException($"Path '{typedPath}' must keep the extension '{extension}' of song '{song.Title}'");
        }

        if (!usedPaths.Add(typedPath) && !removedPaths.Remove(typedPath))
        {
            throw new ValidationException($"Path '{typedPath}' is already used by another song on device '{device.Name}'");
        }

        return typedPath;
    }

    /// <summary>
    /// Groups the typed paths by device, and then by copy or song, rejecting the ones of unknown songs or
    /// devices and copies or songs with more than one path on a device.
    /// </summary>
    private static Dictionary<long, TypedPaths> GroupTypedPaths(
        IReadOnlyList<SongDevicePathInput> paths, List<Song> songs, Dictionary<long, Device> devices)
    {
        var songIds = songs.Select(s => s.Id).ToHashSet();
        var grouped = new Dictionary<long, TypedPaths>();

        foreach (var path in paths)
        {
            if (!devices.ContainsKey(path.DeviceId))
            {
                throw new ValidationException($"Device {path.DeviceId} not found");
            }

            if (!songIds.Contains(path.SongId))
            {
                throw new ValidationException($"Song {path.SongId} is not one of the songs being updated");
            }

            if (!grouped.TryGetValue(path.DeviceId, out var devicePaths))
            {
                grouped[path.DeviceId] = devicePaths = new TypedPaths();
            }

            var added = path.SongDeviceId is { } songDeviceId
                ? devicePaths.ByCopy.TryAdd(songDeviceId, (path.SongId, path.Path.Trim()))
                : devicePaths.BySong.TryAdd(path.SongId, path.Path.Trim());
            if (!added)
            {
                throw new ValidationException($"Song {path.SongId} has more than one path for device {path.DeviceId}");
            }
        }

        return grouped;
    }
}
