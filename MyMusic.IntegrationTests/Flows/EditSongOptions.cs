namespace MyMusic.IntegrationTests.Flows;

public record EditSongOptions(
    string? Title = null,
    int? Year = null,
    string? Lyrics = null,
    int? Rating = null,
    bool? Explicit = null);
