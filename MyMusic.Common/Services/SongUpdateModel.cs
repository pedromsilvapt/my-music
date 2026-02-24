namespace MyMusic.Common.Services;

public record SongUpdateModel
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

public record SongUpdateResult
{
    public required long Id { get; set; }
    public required string Title { get; set; }
    public required string Label { get; set; }
    public required long? Cover { get; set; }
    public required int? Year { get; set; }
    public string? Lyrics { get; set; }
    public decimal? Rating { get; set; }
    public bool Explicit { get; set; }
    public required string RepositoryPath { get; set; }
    public required List<SongUpdateArtist> Artists { get; set; }
    public required SongUpdateAlbum Album { get; set; }
    public required List<SongUpdateGenre> Genres { get; set; }
}

public record SongUpdateArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }
}

public record SongUpdateAlbum
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public SongUpdateAlbumArtist? Artist { get; set; }
}

public record SongUpdateAlbumArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }
}

public record SongUpdateGenre
{
    public required long Id { get; set; }
    public required string Name { get; set; }
}

public record BatchUpdateResult
{
    public required long Id { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public SongUpdateResult? Song { get; set; }
}