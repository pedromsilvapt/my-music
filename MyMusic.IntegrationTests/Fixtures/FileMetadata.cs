namespace MyMusic.IntegrationTests.Fixtures;

public record FileMetadata(
    string Title,
    string Album,
    string[] Artists,
    string[] Genres,
    int? Year,
    int? Track,
    TimeSpan Duration);
