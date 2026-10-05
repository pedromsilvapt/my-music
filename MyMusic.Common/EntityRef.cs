using System.Text.Json.Serialization;

namespace MyMusic.Common;

public record ArtistRef(long? Id = null, string? Name = null);

public record GenreRef(long? Id = null, string? Name = null);

/// <summary>
///     An album is referenced by its name and its album artist: it is found, or created, among the albums of that
///     artist. An empty name stands for the placeholder album. Without an artist, the album artist of the song's
///     current album is kept.
/// </summary>
public record AlbumRef(string? Name = null, ArtistRef? Artist = null)
{
    /// <summary>
    ///     References that exact album instead, ignoring <see cref="Name"/> and <see cref="Artist"/>. Only for
    ///     server-side operations (e.g. merging albums): API requests keep referencing albums by name.
    /// </summary>
    [JsonIgnore]
    public long? Id { get; init; }
}

public record ArtworkRef(long? Id = null, string? Base64 = null);
