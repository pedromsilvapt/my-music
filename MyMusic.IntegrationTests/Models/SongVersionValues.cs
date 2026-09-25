using System.Text.Json;
using System.Text.Json.Serialization;
using MyMusic.Common.Services;

namespace MyMusic.IntegrationTests.Models;

/// <summary>
/// One side (old or new) of the song version modal's diff panels. The panel shows the <c>old</c> or <c>new</c>
/// value of every field changed by the revision, taken from the diff endpoint's metadata, whose shape mirrors
/// <see cref="SongHistoryVersionDiffModel"/> (serialized in camelCase). A <c>null</c> property means the field
/// is absent from the panel (it did not change in that revision), or, in expectations, that it is not asserted.
/// </summary>
public record SongVersionValues
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public string? Title { get; init; }
    public string? Label { get; init; }
    public long? AlbumId { get; init; }
    public long? CoverId { get; init; }
    public int? Year { get; init; }
    public string? Lyrics { get; init; }
    public bool? Explicit { get; init; }
    public long? Size { get; init; }
    public int? Track { get; init; }

    /// <summary>Duration formatted as <c>hh:mm:ss</c>.</summary>
    public string? Duration { get; init; }

    public int? Bitrate { get; init; }
    public long? OwnerId { get; init; }
    public decimal? Rating { get; init; }
    public bool? IsFavorite { get; init; }
    public int? PlayCount { get; init; }
    public string? RepositoryPath { get; init; }
    public string? Checksum { get; init; }
    public string? ChecksumAlgorithm { get; init; }
    public DateTime? AddedAt { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public DateTime? FileModifiedAt { get; init; }

    /// <summary>Cover as a <c>data:{mime};base64,{data}</c> URL.</summary>
    public string? Cover { get; init; }

    public SongHistoryVersionAlbum? Album { get; init; }
    public string? AlbumArtist { get; init; }
    public List<SongHistoryVersionArtist>? Artists { get; init; }
    public List<string>? Genres { get; init; }
    public List<SongHistoryVersionSource>? Sources { get; init; }
    public List<SongHistoryVersionDevice>? Devices { get; init; }

    /// <summary>
    /// Parses a panel's JSON text. Fails on keys not declared in this model, so schema drift is caught.
    /// </summary>
    public static SongVersionValues Parse(string json)
        => JsonSerializer.Deserialize<SongVersionValues>(json, JsonOptions)
           ?? throw new JsonException($"Failed to parse song version values: {json}");
}
