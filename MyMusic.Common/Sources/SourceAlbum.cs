namespace MyMusic.Common.Sources;

public class SourceAlbum
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required SourceSongArtist Artist { get; set; }

    public SourceArtwork? Cover { get; set; }

    public int? Year { get; set; }

    public int? SongsCount { get; set; }

    public string? Link { get; set; }
}