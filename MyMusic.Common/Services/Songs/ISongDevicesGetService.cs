using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Reads where a set of songs is on the user's devices, and where the songs would go on the devices
/// they are not on.
/// </summary>
public interface ISongDevicesGetService
{
    /// <summary>
    /// Returns the songs of <paramref name="ownerId"/> among <paramref name="songIds"/> and, for each of
    /// the user's devices, the copies of those songs it holds and the paths of the others if they were
    /// all added to it now.
    /// </summary>
    Task<SongDevicesResult> GetAsync(long ownerId, IReadOnlyCollection<long> songIds, CancellationToken cancellationToken);
}

/// <summary>
/// The songs, with their artists, and each device's copies and path previews of them.
/// </summary>
public record SongDevicesResult
{
    public required List<Song> Songs { get; init; }
    public required List<SongDevicesDeviceEntry> Devices { get; init; }
}

/// <summary>
/// What a device holds of the songs.
/// </summary>
public record SongDevicesDeviceEntry
{
    public required Device Device { get; init; }

    /// <summary>
    /// The copies of the songs on the device, the ones waiting to be removed included. A song can be at
    /// several paths of the same device.
    /// </summary>
    public required List<SongDevice> Copies { get; init; }

    /// <summary>
    /// The path of each song that has no copy on the device: the device's naming template, made unique
    /// among the device's paths and the other previewed songs.
    /// </summary>
    public required List<SongDevicePathPreview> PathPreviews { get; init; }
}

/// <summary>
/// The path a song would be given on a device it is not on.
/// </summary>
public record SongDevicePathPreview
{
    public required long SongId { get; init; }
    public required string Path { get; init; }
}
