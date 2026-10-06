using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using MyMusic.Common.Extensions;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Default implementation of <see cref="ISongDevicePathPreviewService"/>.
/// </summary>
public class SongDevicePathPreviewService(
    MusicDbContext db,
    ISyncPathResolver pathResolver,
    IOptions<Config> config) : ISongDevicePathPreviewService
{
    /// <inheritdoc />
    public async Task<List<SongDevicePathPreview>> PreviewAsync(long ownerId, IReadOnlyCollection<long> songIds, CancellationToken cancellationToken)
    {
        var songs = await db.Songs
            .AsNoTracking()
            .Where(s => songIds.Contains(s.Id) && s.OwnerId == ownerId)
            .IncludeSongMetadata()
            .AsSplitQuery()
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var devices = await db.Devices
            .AsNoTracking()
            .Where(d => d.OwnerId == ownerId)
            .OrderBy(d => d.Id)
            .ToListAsync(cancellationToken);

        var previews = new List<SongDevicePathPreview>();

        foreach (var device in devices)
        {
            // A song marked for removal is still on the device: adding it back keeps its path
            var songIdsOnDevice = songs
                .Where(s => s.Devices.Any(sd => sd.DeviceId == device.Id))
                .Select(s => s.Id)
                .ToHashSet();

            if (songIdsOnDevice.Count == songs.Count)
            {
                continue;
            }

            var namingStrategy = new TemplateNamingStrategy(
                device.NamingTemplate ?? config.Value.DefaultNamingTemplate);
            var usedPaths = await SongDeviceNewPaths.LoadUsedPathsAsync(db, device.Id, cancellationToken);

            foreach (var song in songs.Where(s => !songIdsOnDevice.Contains(s.Id)))
            {
                previews.Add(new SongDevicePathPreview
                {
                    SongId = song.Id,
                    DeviceId = device.Id,
                    Path = SongDeviceNewPaths.Take(pathResolver, namingStrategy, song, usedPaths),
                });
            }
        }

        return previews;
    }
}
