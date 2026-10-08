namespace MyMusic.IntegrationTests.Fixtures.Models;

/// <summary>
/// A song file that only exists in memory, to be uploaded through the browser.
/// </summary>
public record SongFile(string Name, byte[] Content);
