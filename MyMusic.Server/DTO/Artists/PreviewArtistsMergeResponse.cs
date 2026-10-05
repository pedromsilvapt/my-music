namespace MyMusic.Server.DTO.Artists;

public record PreviewArtistsMergeResponse
{
    /// <summary>
    ///     The songs that merging the artists would change: the ones any of the merged artists performs, together
    ///     with the ones of their albums. A song is counted once.
    /// </summary>
    public required int SongsCount { get; init; }

    /// <summary>
    ///     The albums of the merged artists that would be merged into an album with the same name, instead of just
    ///     becoming albums of the target.
    /// </summary>
    public required int MergedAlbumsCount { get; init; }
}
