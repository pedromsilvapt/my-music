namespace MyMusic.IntegrationTests.Flows;

public record EditSongOptions(
    string? Title = null,
    int? Year = null,
    string? Lyrics = null,
    int? Rating = null,
    bool? Explicit = null,
    string? Album = null,
    string[]? Artists = null,
    string? AlbumArtist = null);
