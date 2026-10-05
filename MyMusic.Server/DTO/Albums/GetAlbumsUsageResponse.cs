namespace MyMusic.Server.DTO.Albums;

public record GetAlbumsUsageResponse
{
    /// <summary>
    ///     The songs that deleting the albums would move to the placeholder album.
    /// </summary>
    public required int SongsCount { get; init; }
}
