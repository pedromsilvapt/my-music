namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Previews the paths songs would be given on the devices they are not on.
/// </summary>
public interface ISongDevicePathPreviewService
{
    /// <summary>
    /// Returns, for each device of <paramref name="ownerId"/> and each of the songs that is not on it, the
    /// path the song would get if all the songs were added to that device now: the device's naming
    /// template, made unique among the device's paths and the other previewed songs.
    /// </summary>
    Task<List<SongDevicePathPreview>> PreviewAsync(long ownerId, IReadOnlyCollection<long> songIds, CancellationToken cancellationToken);
}

/// <summary>
/// The path a song would be given on a device it is not on.
/// </summary>
public record SongDevicePathPreview
{
    public required long SongId { get; init; }
    public required long DeviceId { get; init; }
    public required string Path { get; init; }
}
