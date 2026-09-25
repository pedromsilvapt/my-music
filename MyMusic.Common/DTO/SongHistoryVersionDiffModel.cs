using System.Text.Json.Serialization;

namespace MyMusic.Common.Services;

/// <summary>
/// Diff between two song history revisions as exposed to the version diff
/// viewer. Each field is <c>null</c> (absent) when the field did not change
/// between the selected revision and its predecessor; only fields the
/// underlying <see cref="SongHistoryDelta"/> carries are populated. Absent
/// fields are omitted from the JSON payload via
/// <see cref="JsonIgnoreCondition.WhenWritingNull"/>.
/// <para>
/// The first revision has no predecessor; its <c>Old</c> values are
/// <c>null</c> while <c>New</c> carries the field's value at that revision.
/// </para>
/// </summary>
public record SongHistoryVersionDiffModel
{
    public SongHistoryVersionField<string?>? Title { get; init; }
    public SongHistoryVersionField<string?>? Label { get; init; }
    public SongHistoryVersionField<long>? AlbumId { get; init; }
    public SongHistoryVersionField<long?>? CoverId { get; init; }
    public SongHistoryVersionField<int?>? Year { get; init; }
    public SongHistoryVersionField<string?>? Lyrics { get; init; }
    public SongHistoryVersionField<bool>? Explicit { get; init; }
    public SongHistoryVersionField<long>? Size { get; init; }
    public SongHistoryVersionField<int?>? Track { get; init; }
    public SongHistoryVersionField<string?>? Duration { get; init; }
    public SongHistoryVersionField<int?>? Bitrate { get; init; }
    public SongHistoryVersionField<long>? OwnerId { get; init; }
    public SongHistoryVersionField<decimal?>? Rating { get; init; }
    public SongHistoryVersionField<bool>? IsFavorite { get; init; }
    public SongHistoryVersionField<int>? PlayCount { get; init; }
    public SongHistoryVersionField<string?>? RepositoryPath { get; init; }
    public SongHistoryVersionField<string?>? Checksum { get; init; }
    public SongHistoryVersionField<string?>? ChecksumAlgorithm { get; init; }
    public SongHistoryVersionField<DateTime?>? AddedAt { get; init; }
    public SongHistoryVersionField<DateTime>? CreatedAt { get; init; }
    public SongHistoryVersionField<DateTime>? ModifiedAt { get; init; }
    public SongHistoryVersionField<DateTime?>? FileModifiedAt { get; init; }
    public SongHistoryVersionField<string?>? Cover { get; init; }
    public SongHistoryVersionField<SongHistoryVersionAlbum?>? Album { get; init; }
    public SongHistoryVersionField<string?>? AlbumArtist { get; init; }
    public SongHistoryVersionField<List<SongHistoryVersionArtist>?>? Artists { get; init; }
    public SongHistoryVersionField<List<string>?>? Genres { get; init; }
    public SongHistoryVersionField<List<SongHistoryVersionSource>?>? Sources { get; init; }
    public SongHistoryVersionField<List<SongHistoryVersionDevice>?>? Devices { get; init; }
}

/// <summary>
/// A single field's old/new values in a version diff. Both values are
/// nullable to support the first-revision case where <see cref="Old"/> is
/// <c>null</c> (no previous version exists).
/// </summary>
public record SongHistoryVersionField<T>
{
    public required T? Old { get; init; }
    public required T? New { get; init; }
}

public record SongHistoryVersionAlbum
{
    public required string Name { get; init; }
    public string? ArtistName { get; init; }
}

public record SongHistoryVersionArtist
{
    public required string Name { get; init; }
}

public record SongHistoryVersionSource
{
    public required string Name { get; init; }
}

public record SongHistoryVersionDevice
{
    public required string DevicePath { get; init; }
    public string? SyncAction { get; init; }
}