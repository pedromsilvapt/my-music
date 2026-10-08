namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Validates a device naming template and previews the paths it gives to the songs of a device, or to a song.
/// </summary>
public interface IDeviceNamingPreviewService
{
    /// <summary>
    /// Validates <paramref name="namingTemplate"/> (blank = the server's default template) and, when
    /// <paramref name="deviceId"/> is given, computes the path each song of that device would get from it,
    /// the same way a sync does. When <paramref name="songId"/> is given, computes the path that song would
    /// get when added to a device with that template. Nothing is saved. Returns <c>null</c> when the device
    /// or the song is not one of <paramref name="ownerId"/>'s.
    /// </summary>
    Task<DeviceNamingPreviewResult?> PreviewAsync(
        long ownerId,
        long? deviceId,
        long? songId,
        string? namingTemplate,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a naming template preview. When <see cref="Errors"/> has items, there are no songs and no
/// <see cref="Song"/>.
/// </summary>
public record DeviceNamingPreviewResult
{
    /// <summary>The template used by devices that have none.</summary>
    public required string DefaultNamingTemplate { get; init; }
    public required List<DeviceNamingPreviewError> Errors { get; init; }

    /// <summary>How many files of the device were previewed.</summary>
    public required int Total { get; init; }

    /// <summary>How many of them the template gives a path other than their current one.</summary>
    public required int Renamed { get; init; }
    public required List<DeviceNamingPreviewSong> Songs { get; init; }

    /// <summary>The path of the previewed song, when the preview was asked for a song.</summary>
    public DeviceNamingPreviewSongPath? Song { get; init; }
}

/// <summary>
/// A syntax error of a naming template. Lines and columns are 1-based; the end is exclusive.
/// </summary>
public record DeviceNamingPreviewError
{
    public required string Message { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required int EndLine { get; init; }
    public required int EndColumn { get; init; }
}

/// <summary>
/// A file of the device, with the path the previewed template gives it.
/// </summary>
public record DeviceNamingPreviewSong
{
    public required long SongDeviceId { get; init; }
    public required long SongId { get; init; }
    public required string Title { get; init; }
    public required string CurrentPath { get; init; }
    public required string NewPath { get; init; }
    public required bool Changed { get; init; }

    /// <summary>Why the template could not be rendered for this song, if it could not.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// The path the previewed template gives to a song added to a device.
/// </summary>
public record DeviceNamingPreviewSongPath
{
    /// <summary>The path, unless the template could not be rendered for the song.</summary>
    public string? Path { get; init; }

    /// <summary>Why the template could not be rendered for the song, if it could not.</summary>
    public string? Error { get; init; }
}
