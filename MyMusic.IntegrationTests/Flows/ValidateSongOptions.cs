namespace MyMusic.IntegrationTests.Flows;

public record ValidateSongOptions(
    string? Title = null,
    string[]? Artists = null,
    string? Album = null,
    int? Year = null,
    bool? Explicit = null,
    string[]? Genres = null);
