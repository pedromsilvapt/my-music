namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Changes which devices hold a set of songs, and the paths of the songs on those devices.
/// </summary>
public interface ISongDevicesUpdateService
{
    /// <summary>
    /// Applies <paramref name="input"/> to the songs and devices of <paramref name="ownerId"/> as one
    /// operation: nothing is saved when any part of it is invalid.
    /// </summary>
    /// <exception cref="ValidationException">A typed path is invalid, taken, or of a song that is not on the device.</exception>
    Task UpdateAsync(long ownerId, SongDevicesUpdateInput input, CancellationToken cancellationToken);
}

/// <summary>
/// The devices to add the songs to or remove them from, and the paths typed for songs on devices.
/// </summary>
public record SongDevicesUpdateInput
{
    public required IReadOnlyList<long> SongIds { get; init; }
    public required IReadOnlyList<SongDeviceMembershipInput> Updates { get; init; }
    public IReadOnlyList<SongDevicePathInput> Paths { get; init; } = [];
}

/// <summary>
/// Adds all the songs to a device (<see cref="Include"/>) or removes them from it.
/// </summary>
public record SongDeviceMembershipInput
{
    public required long DeviceId { get; init; }
    public required bool Include { get; init; }
}

/// <summary>
/// A path typed by the user for a song on a device. The device creates the file at (or renames it to)
/// this exact path on its next sync. From then on it is a path like any other: when the song changes,
/// the device's naming template is applied again.
/// </summary>
public record SongDevicePathInput
{
    public required long SongId { get; init; }
    public required long DeviceId { get; init; }
    public required string Path { get; init; }
}
