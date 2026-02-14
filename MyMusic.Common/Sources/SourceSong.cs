namespace MyMusic.Common.Sources;

public class SourceSong
{
    public required string Id { get; set; }

    public required string Title { get; set; }

    public SourceSongAlbum Album { get; set; } = null!;

    public SourceArtwork? Cover { get; set; }

    public int? Year { get; set; }

    public string? Lyrics { get; set; }

    public bool Explicit { get; set; }

    public long Size { get; set; }

    public int? Track { get; set; }

    public TimeSpan Duration { get; set; }

    public required List<SourceSongArtist> Artists { get; set; }

    public required List<string> Genres { get; set; }

    public string? Link { get; set; } = null;

    public decimal Price { get; set; } = 0;
}