using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using MyMusic.Common.Extensions;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Default implementation of <see cref="ISongDevicesGetService"/>.
/// </summary>
public class SongDevicesGetService(
    MusicDbContext db,
    ISyncPathResolver pathResolver,
    IOptions<Config> config) : ISongDevicesGetService
{
    /// <inheritdoc />
    public async Task<SongDevicesResult> GetAsync(long ownerId, IReadOnlyCollection<long> songIds, CancellationToken cancellationToken)
    {
        var songs = await db.Songs
            .AsNoTracking()
            .Where(s => songIds.Contains(s.Id) && s.OwnerId == ownerId)
            .IncludeSongMetadata()
            .AsSplitQuery()
            .OrderBy(s => s.Title)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var devices = await db.Devices
            .AsNoTracking()
            .Where(d => d.OwnerId == ownerId)
            .OrderBy(d => d.Id)
            .ToListAsync(cancellationToken);

        var entries = new List<SongDevicesDeviceEntry>();

        foreach (var device in devices)
        {
            // A song marked for removal is still on the device: adding it back keeps its path
            var copies = songs
                .SelectMany(s => s.Devices.Where(sd => sd.DeviceId == device.Id))
                .OrderBy(sd => sd.DevicePath, StringComparer.Ordinal)
                .ToList();
            var songIdsOnDevice = copies.Select(sd => sd.SongId).ToHashSet();
            var previews = new List<SongDevicePathPreview>();

            if (songIdsOnDevice.Count < songs.Count)
            {
                var namingStrategy = new TemplateNamingStrategy(
                    device.NamingTemplate ?? config.Value.DefaultNamingTemplate);
                var usedPaths = await SongDeviceNewPaths.LoadUsedPathsAsync(db, device.Id, cancellationToken);

                foreach (var song in songs.Where(s => !songIdsOnDevice.Contains(s.Id)))
                {
                    previews.Add(new SongDevicePathPreview
                    {
                        SongId = song.Id,
                        Path = SongDeviceNewPaths.Take(pathResolver, namingStrategy, song, usedPaths),
                    });
                }
            }

            entries.Add(new SongDevicesDeviceEntry { Device = device, Copies = copies, PathPreviews = previews });
        }

        return new SongDevicesResult { Songs = songs, Devices = entries };
    }
}
