namespace MyMusic.Common;

public record ArtistRef(long? Id = null, string? Name = null);

public record GenreRef(long? Id = null, string? Name = null);

public record AlbumRef(long? Id = null, string? Name = null, string? ArtistName = null);

public record ArtworkRef(long? Id = null, string? Base64 = null);
