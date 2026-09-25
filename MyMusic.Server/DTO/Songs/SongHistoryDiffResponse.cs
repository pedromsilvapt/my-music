using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;

namespace MyMusic.Server.DTO.Songs;

/// <summary>
/// Response for the song history version diff endpoint
/// (<c>GET /songs/{songId}/history/{historyId}/diff</c>). Contains the
/// field-by-field metadata diff between the selected revision and its
/// predecessor, plus the revision metadata (numbers and timestamps).
/// </summary>
public record SongHistoryDiffResponse
{
    /// <summary>The field-by-field metadata diff (mirrors the client's <c>SongMetadataDiff</c>).</summary>
    public required SongHistoryDiffMetadata Metadata { get; init; }

    /// <summary>The timestamp of the older (previous) snapshot, or <c>null</c> when the selected entry is the first revision.</summary>
    public DateTime? OldVersionDate { get; init; }

    /// <summary>The timestamp of the selected (newer) snapshot.</summary>
    public required DateTime NewVersionDate { get; init; }

    /// <summary>The revision number of the older snapshot, or <c>null</c> when the selected entry is the first revision.</summary>
    public int? OldRevision { get; init; }

    /// <summary>The revision number of the selected snapshot.</summary>
    public required int NewRevision { get; init; }

    /// <summary>
    /// Maps a <see cref="SongHistoryVersionDiffResult"/> (from Common) to this response DTO.
    /// </summary>
    public static SongHistoryDiffResponse FromResult(SongHistoryVersionDiffResult result) =>
        new()
        {
            Metadata = SongHistoryDiffMetadata.FromModel(result.Metadata),
            OldVersionDate = result.OldVersionDate,
            NewVersionDate = result.NewVersionDate,
            OldRevision = result.OldRevision,
            NewRevision = result.NewRevision,
        };
}

/// <summary>
/// Server-side DTO mirroring the client's <c>SongMetadataDiff</c> shape, but with
/// nullable <c>Old</c>/<c>New</c> values to support the first-revision case where
/// there is no previous version. Only fields that changed in the selected
/// revision are populated; absent fields are <c>null</c> and omitted from the
/// JSON payload.
/// </summary>
public record SongHistoryDiffMetadata
{
    public SongHistoryDiffField<string?>? Title { get; init; }
    public SongHistoryDiffField<string?>? Label { get; init; }
    public SongHistoryDiffField<long>? AlbumId { get; init; }
    public SongHistoryDiffField<long?>? CoverId { get; init; }
    public SongHistoryDiffField<int?>? Year { get; init; }
    public SongHistoryDiffField<string?>? Lyrics { get; init; }
    public SongHistoryDiffField<bool>? Explicit { get; init; }
    public SongHistoryDiffField<long>? Size { get; init; }
    public SongHistoryDiffField<int?>? Track { get; init; }
    public SongHistoryDiffField<string?>? Duration { get; init; }
    public SongHistoryDiffField<int?>? Bitrate { get; init; }
    public SongHistoryDiffField<long>? OwnerId { get; init; }
    public SongHistoryDiffField<decimal?>? Rating { get; init; }
    public SongHistoryDiffField<bool>? IsFavorite { get; init; }
    public SongHistoryDiffField<int>? PlayCount { get; init; }
    public SongHistoryDiffField<string?>? RepositoryPath { get; init; }
    public SongHistoryDiffField<string?>? Checksum { get; init; }
    public SongHistoryDiffField<string?>? ChecksumAlgorithm { get; init; }
    public SongHistoryDiffField<DateTime?>? AddedAt { get; init; }
    public SongHistoryDiffField<DateTime>? CreatedAt { get; init; }
    public SongHistoryDiffField<DateTime>? ModifiedAt { get; init; }
    public SongHistoryDiffField<DateTime?>? FileModifiedAt { get; init; }
    public SongHistoryDiffField<string?>? Cover { get; init; }
    public SongHistoryDiffField<SongHistoryDiffAlbum?>? Album { get; init; }
    public SongHistoryDiffField<string?>? AlbumArtist { get; init; }
    public SongHistoryDiffField<List<SongHistoryDiffArtist>?>? Artists { get; init; }
    public SongHistoryDiffField<List<string>?>? Genres { get; init; }
    public SongHistoryDiffField<List<SongHistoryDiffSource>?>? Sources { get; init; }
    public SongHistoryDiffField<List<SongHistoryDiffDevice>?>? Devices { get; init; }

    public static SongHistoryDiffMetadata FromModel(SongHistoryVersionDiffModel model) => new()
    {
        Title = MapScalar(model.Title),
        Label = MapScalar(model.Label),
        AlbumId = MapStruct(model.AlbumId),
        CoverId = MapScalar(model.CoverId),
        Year = MapScalar(model.Year),
        Lyrics = MapScalar(model.Lyrics),
        Explicit = MapStruct(model.Explicit),
        Size = MapStruct(model.Size),
        Track = MapScalar(model.Track),
        Duration = MapScalar(model.Duration),
        Bitrate = MapScalar(model.Bitrate),
        OwnerId = MapStruct(model.OwnerId),
        Rating = MapScalar(model.Rating),
        IsFavorite = MapStruct(model.IsFavorite),
        PlayCount = MapStruct(model.PlayCount),
        RepositoryPath = MapScalar(model.RepositoryPath),
        Checksum = MapScalar(model.Checksum),
        ChecksumAlgorithm = MapScalar(model.ChecksumAlgorithm),
        AddedAt = MapScalar(model.AddedAt),
        CreatedAt = MapStruct(model.CreatedAt),
        ModifiedAt = MapStruct(model.ModifiedAt),
        FileModifiedAt = MapScalar(model.FileModifiedAt),
        Cover = MapScalar(model.Cover),
        Album = MapAlbum(model.Album),
        AlbumArtist = MapScalar(model.AlbumArtist),
        Artists = MapArtists(model.Artists),
        Genres = MapScalar(model.Genres),
        Sources = MapSources(model.Sources),
        Devices = MapDevices(model.Devices),
    };

    private static SongHistoryDiffField<T?>? MapScalar<T>(SongHistoryVersionField<T?>? field)
        => field is null ? null : new() { Old = field.Old, New = field.New };

    private static SongHistoryDiffField<T>? MapStruct<T>(SongHistoryVersionField<T>? field)
        where T : struct
        => field is null ? null : new() { Old = field.Old, New = field.New };

    private static SongHistoryDiffField<SongHistoryDiffAlbum?>? MapAlbum(
        SongHistoryVersionField<SongHistoryVersionAlbum?>? field)
        => field is null ? null : new()
        {
            Old = SongHistoryDiffAlbum.FromModel(field.Old),
            New = SongHistoryDiffAlbum.FromModel(field.New),
        };

    private static SongHistoryDiffField<List<SongHistoryDiffArtist>?>? MapArtists(
        SongHistoryVersionField<List<SongHistoryVersionArtist>?>? field)
        => field is null ? null : new()
        {
            Old = field.Old?.Select(SongHistoryDiffArtist.FromModel).ToList(),
            New = field.New?.Select(SongHistoryDiffArtist.FromModel).ToList(),
        };

    private static SongHistoryDiffField<List<SongHistoryDiffSource>?>? MapSources(
        SongHistoryVersionField<List<SongHistoryVersionSource>?>? field)
        => field is null ? null : new()
        {
            Old = field.Old?.Select(SongHistoryDiffSource.FromModel).ToList(),
            New = field.New?.Select(SongHistoryDiffSource.FromModel).ToList(),
        };

    private static SongHistoryDiffField<List<SongHistoryDiffDevice>?>? MapDevices(
        SongHistoryVersionField<List<SongHistoryVersionDevice>?>? field)
        => field is null ? null : new()
        {
            Old = field.Old?.Select(SongHistoryDiffDevice.FromModel).ToList(),
            New = field.New?.Select(SongHistoryDiffDevice.FromModel).ToList(),
        };
}

public record SongHistoryDiffField<T>
{
    public required T? Old { get; init; }
    public required T? New { get; init; }
}

public record SongHistoryDiffAlbum
{
    public string? Name { get; init; }
    public string? ArtistName { get; init; }

    public static SongHistoryDiffAlbum? FromModel(SongHistoryVersionAlbum? album) =>
        album == null ? null : new()
        {
            Name = album.Name,
            ArtistName = album.ArtistName,
        };
}

public record SongHistoryDiffArtist
{
    public string? Name { get; init; }

    public static SongHistoryDiffArtist FromModel(SongHistoryVersionArtist artist) =>
        new() { Name = artist.Name };
}

public record SongHistoryDiffSource
{
    public string? Name { get; init; }

    public static SongHistoryDiffSource FromModel(SongHistoryVersionSource source) =>
        new() { Name = source.Name };
}

public record SongHistoryDiffDevice
{
    public string? DevicePath { get; init; }
    public string? SyncAction { get; init; }

    public static SongHistoryDiffDevice FromModel(SongHistoryVersionDevice device) =>
        new() { DevicePath = device.DevicePath, SyncAction = device.SyncAction };
}