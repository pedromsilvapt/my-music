namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// What to change in an album through its editor. A <see langword="null"/> value leaves that field as it is.
/// </summary>
public record EditAlbumOptions(string? Name = null, int? Year = null);
