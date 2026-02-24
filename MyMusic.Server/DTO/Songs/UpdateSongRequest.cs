namespace MyMusic.Server.DTO.Songs;

public record UpdateSongRequest
{
    public required long SongId { get; set; }
    public string? Title { get; set; }
    public long? AlbumId { get; set; }
    public string? AlbumName { get; set; }
    public long? AlbumArtistId { get; set; }
    public string? AlbumArtistName { get; set; }
    public List<long>? ArtistIds { get; set; }
    public List<string>? ArtistNames { get; set; }
    public List<long>? GenreIds { get; set; }
    public List<string>? GenreNames { get; set; }
    public int? Year { get; set; }
    public string? Lyrics { get; set; }
    public decimal? Rating { get; set; }
    public bool? Explicit { get; set; }
    public string? Cover { get; set; }
}

public record BatchUpdateSongsRequest
{
    public required List<long> SongIds { get; set; }
    public required SongPatch Patch { get; set; }
}

public record SongPatch
{
    public string? Title { get; set; }
    public long? AlbumId { get; set; }
    public string? AlbumName { get; set; }
    public long? AlbumArtistId { get; set; }
    public string? AlbumArtistName { get; set; }
    public List<long>? ArtistIds { get; set; }
    public List<string>? ArtistNames { get; set; }
    public List<long>? GenreIds { get; set; }
    public List<string>? GenreNames { get; set; }
    public int? Year { get; set; }
    public string? Lyrics { get; set; }
    public decimal? Rating { get; set; }
    public bool? Explicit { get; set; }
    public string? Cover { get; set; }
}