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

    public string SearchableText =>
        (Title ?? "") + " " +
        (Album?.Name ?? "") + " " +
        string.Join(" ", Artists?.Select(a => a.Name) ?? []) + " " +
        string.Join(" ", Genres ?? []);

    public int DurationSeconds => (int)Duration.TotalSeconds;

    public string DurationCategory => Duration.TotalSeconds switch
    {
        < 180 => "Short",
        < 360 => "Medium",
        _ => "Long",
    };

    public bool HasLyrics => !string.IsNullOrEmpty(Lyrics);
    public int ArtistCount => Artists?.Count ?? 0;
    public int GenreCount => Genres?.Count ?? 0;
}