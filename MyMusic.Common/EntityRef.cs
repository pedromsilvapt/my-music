namespace MyMusic.Common;

public record ArtistRef(long? Id = null, string? Name = null);

public record GenreRef(long? Id = null, string? Name = null);

/// <summary>
///     An album is referenced by its name and its album artist, never by id: it is found, or created, among the
///     albums of that artist. An empty name stands for the placeholder album. Without an artist, the album artist
///     of the song's current album is kept.
/// </summary>
public record AlbumRef(string? Name = null, ArtistRef? Artist = null);

public record ArtworkRef(long? Id = null, string? Base64 = null);
