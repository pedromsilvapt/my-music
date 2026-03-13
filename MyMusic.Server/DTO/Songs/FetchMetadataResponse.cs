namespace MyMusic.Server.DTO.Songs;

public record FetchMetadataResponse
{
    public required SongMetadataDiff Metadata { get; set; }
}

public record SongMetadataDiff
{
    public SongMetadataField<string>? Title { get; set; }
    public SongMetadataField<int>? Year { get; set; }
    public SongMetadataField<string>? Lyrics { get; set; }
    public SongMetadataField<decimal>? Rating { get; set; }
    public SongMetadataField<bool>? Explicit { get; set; }
    public SongMetadataField<string>? Cover { get; set; }
    public SongMetadataField<SongMetadataAlbum>? Album { get; set; }
    public SongMetadataField<string>? AlbumArtist { get; set; }
    public SongMetadataField<List<SongMetadataArtist>>? Artists { get; set; }
    public SongMetadataField<List<string>>? Genres { get; set; }
}

public record SongMetadataField<T>
{
    public required T Old { get; set; }
    public required T New { get; set; }
}

public record SongMetadataAlbum
{
    public string Name { get; set; } = null!;
    public string? ArtistName { get; set; }
}

public record SongMetadataArtist
{
    public string Name { get; set; } = null!;
}