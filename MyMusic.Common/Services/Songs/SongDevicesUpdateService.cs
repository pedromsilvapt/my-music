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

        var deviceIds = input.Updates.Select(u => u.DeviceId)
            .Concat(input.Paths.Select(p => p.DeviceId))
            .Distinct()
            .ToList();
        var devices = await db.Devices
            .Where(d => deviceIds.Contains(d.Id) && d.OwnerId == ownerId)
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var typedPaths = GroupTypedPaths(input.Paths, songs, devices);
        var memberships = input.Updates
            .GroupBy(u => u.DeviceId)
            .ToDictionary(g => g.Key, g => (bool?)g.Last().Include);

        foreach (var deviceId in deviceIds)
        {
            if (!devices.TryGetValue(deviceId, out var device))
            {
                continue;
            }

            await UpdateDeviceAsync(device, songs, memberships.GetValueOrDefault(deviceId),
                typedPaths.GetValueOrDefault(deviceId) ?? [], cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies the membership change (<paramref name="include"/>, null when the device only has typed
    /// paths) and the typed paths of one device.
    /// </summary>
    private async Task UpdateDeviceAsync(
        Device device,
        List<Song> songs,
        bool? include,
        Dictionary<long, string> typedPaths,
        CancellationToken cancellationToken)
    {
        var songIds = songs.Select(s => s.Id).ToList();

        var existingDict = await db.SongDevices
            .Where(sd => sd.SongId != null && songIds.Contains(sd.SongId.Value) && sd.DeviceId == device.Id)
            .ToDictionaryAsync(sd => sd.SongId!.Value, cancellationToken);

        var usedPaths = await SongDeviceNewPaths.LoadUsedPathsAsync(db, device.Id, cancellationToken);

        // The typed paths are taken first, so the generated paths of the other songs are made unique around them
        var songsInOrder = songs.OrderByDescending(s => typedPaths.ContainsKey(s.Id)).ToList();

        TemplateNamingStrategy? namingStrategy = null;

        foreach (var song in songsInOrder)
        {
            var existing = existingDict.GetValueOrDefault(song.Id);
            var typedPath = typedPaths.GetValueOrDefault(song.Id);

            if (include == true && existing is null)
            {
                string devicePath;
                if (typedPath != null)
                {
                    devicePath = TakeTypedPath(typedPath, song, device, usedPaths);
                }
                else
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
                    RequestedPath = typedPath != null ? devicePath : null,
                    SyncAction = SongSyncAction.Download,
                    SyncActionReason = "Song included on device",
                    AddedAt = DateTime.UtcNow,
                });
                continue;
            }

            if (include == false && existing is not null)
            {
                if (typedPath != null)
                {
                    throw new ValidationException(
                        $"Cannot set the path of song '{song.Title}' on device '{device.Name}': the song is being removed from it");
                }

                if (existing is { SyncAction: SongSyncAction.Download, LastSyncedModifiedAt: null })
                {
                    db.SongDevices.Remove(existing);
                }
                else
                {
                    existing.SyncAction = SongSyncAction.Remove;
                    existing.SyncActionReason = "Song excluded from device";
                }

                continue;
            }

            if (include == true && existing is { SyncAction: SongSyncAction.Remove })
            {
                existing.SyncAction = null;
                existing.SyncActionReason = null;
            }

            if (typedPath != null)
            {
                if (existing is null or { SyncAction: SongSyncAction.Remove })
                {
                    throw new ValidationException(
                        $"Cannot set the path of song '{song.Title}' on device '{device.Name}': the song is not on it");
                }

                ApplyTypedPath(existing, typedPath, song, device, usedPaths);
            }
        }
    }

    /// <summary>
    /// Sets the typed path of a song that is on the device. A file the device already holds keeps its
    /// <see cref="SongDevice.DevicePath"/> (where the device reports it) until the next sync renames it.
    /// </summary>
    private static void ApplyTypedPath(SongDevice songDevice, string typedPath, Song song, Device device, HashSet<string> usedPaths)
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

        var neverDownloaded = songDevice is { SyncAction: SongSyncAction.Download, LastSyncedModifiedAt: null };
        if (neverDownloaded)
        {
            usedPaths.Remove(songDevice.DevicePath);
        }

        var path = TakeTypedPath(typedPath, song, device, usedPaths);

        songDevice.RequestedPath = path;
        if (neverDownloaded)
        {
            songDevice.DevicePath = path;
        }
    }

    /// <summary>
    /// Validates a typed path and adds it to <paramref name="usedPaths"/>.
    /// </summary>
    private static string TakeTypedPath(string typedPath, Song song, Device device, HashSet<string> usedPaths)
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

        if (!usedPaths.Add(typedPath))
        {
            throw new ValidationException($"Path '{typedPath}' is already used by another song on device '{device.Name}'");
        }

        return typedPath;
    }

    /// <summary>
    /// Groups the typed paths by device and song, rejecting the ones of unknown songs or devices and
    /// songs with more than one path on a device.
    /// </summary>
    private static Dictionary<long, Dictionary<long, string>> GroupTypedPaths(
        IReadOnlyList<SongDevicePathInput> paths, List<Song> songs, Dictionary<long, Device> devices)
    {
        var songIds = songs.Select(s => s.Id).ToHashSet();
        var grouped = new Dictionary<long, Dictionary<long, string>>();

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
                grouped[path.DeviceId] = devicePaths = [];
            }

            if (!devicePaths.TryAdd(path.SongId, path.Path.Trim()))
            {
                throw new ValidationException($"Song {path.SongId} has more than one path for device {path.DeviceId}");
            }
        }

        return grouped;
    }
}
