namespace MyMusic.Common.Services;

/// <summary>
/// Model for metadata diff that can be shared between server and background services.
/// This mirrors the structure from MyMusic.Server.DTO.Songs for compatibility.
/// </summary>
public record MetadataDiffModel
{
    public MetadataField<string>? Title { get; set; }
    public MetadataField<int>? Year { get; set; }
    public MetadataField<string>? Lyrics { get; set; }
    public MetadataField<decimal>? Rating { get; set; }
    public MetadataField<bool>? Explicit { get; set; }
    public MetadataField<string>? Cover { get; set; }
    public MetadataField<MetadataAlbumModel>? Album { get; set; }
    public MetadataField<string>? AlbumArtist { get; set; }
    public MetadataField<List<MetadataArtistModel>>? Artists { get; set; }
    public MetadataField<List<string>>? Genres { get; set; }
}

public record MetadataField<T>
{
    public required T Old { get; set; }
    public required T New { get; set; }
}

public record MetadataAlbumModel
{
    public string Name { get; set; } = null!;
    public string? ArtistName { get; set; }
}

public record MetadataArtistModel
{
    public string Name { get; set; } = null!;
}
