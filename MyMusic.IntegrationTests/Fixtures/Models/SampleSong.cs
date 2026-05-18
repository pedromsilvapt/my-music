namespace MyMusic.IntegrationTests.Fixtures.Models;

public record SampleSong(
    string? Title = null,
    string? Album = null,
    string[]? Artists = null,
    string[]? Genres = null,
    int? Year = null,
    string? Lyrics = null,
    string? AlbumArtist = null,
    long[]? DeviceIds = null);
