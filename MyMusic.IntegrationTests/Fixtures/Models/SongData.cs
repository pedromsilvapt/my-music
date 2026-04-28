namespace MyMusic.IntegrationTests.Fixtures.Models;

public record SongData(long Id, string Title, int? Year, Dictionary<long, string>? DevicePaths = null);
