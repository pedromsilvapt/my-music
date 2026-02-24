namespace MyMusic.Server.DTO.Songs;

public record AutocompleteAlbumsResponse
{
    public required List<AutocompleteAlbumItem> Albums { get; set; }
}

public record AutocompleteAlbumItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public string? ArtistName { get; set; }
}

public record AutocompleteArtistsResponse
{
    public required List<AutocompleteArtistItem> Artists { get; set; }
}

public record AutocompleteArtistItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
}

public record AutocompleteGenresResponse
{
    public required List<AutocompleteGenreItem> Genres { get; set; }
}

public record AutocompleteGenreItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
}