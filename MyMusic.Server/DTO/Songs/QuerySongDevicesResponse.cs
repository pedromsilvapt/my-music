using MyMusic.Common.Services.Songs;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Songs;

public record QuerySongDevicesResponse
{
    public required List<QuerySongDevicesSong> Songs { get; init; }
    public required List<QuerySongDevicesDevice> Devices { get; init; }

    public static QuerySongDevicesResponse FromResult(SongDevicesResult result) =>
        new()
        {
            Songs = result.Songs.Select(QuerySongDevicesSong.FromEntity).ToList(),
            Devices = result.Devices.Select(QuerySongDevicesDevice.FromEntry).ToList(),
        };
}

public record QuerySongDevicesSong
{
    public required long Id { get; init; }
    public required string Title { get; init; }
    public required List<ListSongsArtist> Artists { get; init; }

    public static QuerySongDevicesSong FromEntity(Entities.Song song) =>
        new()
        {
            Id = song.Id,
            Title = song.Title,
            Artists = song.Artists.Select(a => ListSongsArtist.FromEntity(a.Artist)).ToList(),
        };
}

public record QuerySongDevicesDevice
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }

    /// <summary>
    /// The copies of the songs on the device. A song can be at several paths of the same device.
    /// </summary>
    public required List<QuerySongDevicesCopy> Copies { get; init; }

    /// <summary>The path each song that is not on the device would be given on it.</summary>
    public required List<QuerySongDevicesPathPreview> PathPreviews { get; init; }

    public static QuerySongDevicesDevice FromEntry(SongDevicesDeviceEntry entry) =>
        new()
        {
            Id = entry.Device.Id,
            Name = entry.Device.Name,
            Icon = entry.Device.Icon,
            Color = entry.Device.Color,
            Copies = entry.Copies.Select(QuerySongDevicesCopy.FromEntity).ToList(),
            PathPreviews = entry.PathPreviews
                .Select(p => new QuerySongDevicesPathPreview { SongId = p.SongId, Path = p.Path })
                .ToList(),
        };
}

public record QuerySongDevicesCopy
{
    public required long SongDeviceId { get; init; }
    public required long SongId { get; init; }
    public required string Path { get; init; }

    /// <summary>The path the user typed for the copy, which the next sync applies on the device.</summary>
    public string? RequestedPath { get; init; }
    public string? SyncAction { get; init; }

    public static QuerySongDevicesCopy FromEntity(Entities.SongDevice songDevice) =>
        new()
        {
            SongDeviceId = songDevice.Id,
            SongId = songDevice.SongId!.Value,
            Path = songDevice.DevicePath,
            RequestedPath = songDevice.RequestedPath,
            SyncAction = songDevice.SyncAction?.ToString(),
        };
}

public record QuerySongDevicesPathPreview
{
    public required long SongId { get; init; }
    public required string Path { get; init; }
}
