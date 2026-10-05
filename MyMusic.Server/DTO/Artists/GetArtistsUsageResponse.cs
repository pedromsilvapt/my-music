namespace MyMusic.Server.DTO.Artists;

public record GetArtistsUsageResponse
{
    /// <summary>
    ///     The songs that deleting the artists would change: the ones any of them performs, together with the ones of
    ///     their albums. A song is counted once.
    /// </summary>
    public required int SongsCount { get; init; }
}
