namespace MyMusic.Server.DTO.Songs;

public record UpdateSongResponse
{
    public required UpdateSongItem Song { get; set; }
}

public record UpdateSongItem
{
    public required long Id { get; set; }
    public required string Title { get; set; }
    public required string Label { get; set; }
    public required long? Cover { get; set; }
    public required int? Year { get; set; }
    public string? Lyrics { get; set; }
    public decimal? Rating { get; set; }
    public bool Explicit { get; set; }
    public required List<UpdateSongArtist> Artists { get; set; }
    public required UpdateSongAlbum Album { get; set; }
    public required List<UpdateSongGenre> Genres { get; set; }
    public string? RepositoryPath { get; set; }
}

public record UpdateSongArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }
}

public record UpdateSongAlbum
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public UpdateSongAlbumArtist? Artist { get; set; }
}

public record UpdateSongAlbumArtist
{
    public required long Id { get; set; }
    public required string Name { get; set; }
}

public record UpdateSongGenre
{
    public required long Id { get; set; }
    public required string Name { get; set; }
}

public record BatchUpdateSongsResponse
{
    public required List<BatchUpdateSongResult> Songs { get; set; }
}

public record BatchUpdateSongResult
{
    public required long Id { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public UpdateSongItem? Song { get; set; }
}