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
    /// <exception cref="ValidationException">
    /// A typed path is invalid, taken, or of a song that is not on the device, or a copy is not one of the songs' copies.
    /// </exception>
    Task UpdateAsync(long ownerId, SongDevicesUpdateInput input, CancellationToken cancellationToken);
}

/// <summary>
/// The devices to add the songs to or remove them from, the copies to remove or restore one by one, and
/// the paths typed for songs on devices.
/// </summary>
public record SongDevicesUpdateInput
{
    public required IReadOnlyList<long> SongIds { get; init; }
    public required IReadOnlyList<SongDeviceMembershipInput> Updates { get; init; }
    public IReadOnlyList<SongDevicePathInput> Paths { get; init; } = [];
    public IReadOnlyList<SongDeviceCopyInput> Copies { get; init; } = [];
}

/// <summary>
/// Adds all the songs to a device (<see cref="Include"/>) or removes them from it. Removing a song removes
/// every copy of it. Adding a song the device already holds restores its copies only when all of them are
/// waiting to be removed.
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

    /// <summary>
    /// The copy (<see cref="Entities.SongDevice"/>) the path is for. Null for a song being added, or for
    /// the only copy of a song: a song that is more than once on the device needs it.
    /// </summary>
    public long? SongDeviceId { get; init; }

    public required string Path { get; init; }
}

/// <summary>
/// Removes one copy of a song from its device, or restores (<see cref="Include"/>) a copy waiting to be removed.
/// </summary>
public record SongDeviceCopyInput
{
    public required long SongDeviceId { get; init; }
    public required bool Include { get; init; }
}
