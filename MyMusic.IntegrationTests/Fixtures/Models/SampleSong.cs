namespace MyMusic.IntegrationTests.Fixtures.Models;

public record SampleSong(
    string Title,
    string Album,
    string[] Artists,
    string[] Genres,
    int? Year,
    long[]? DeviceIds = null);
