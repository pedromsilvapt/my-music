namespace MyMusic.Server.DTO.Albums;

public record PreviewAlbumsMergeResponse
{
    /// <summary>
    ///     The songs that merging the albums would move to the target album.
    /// </summary>
    public required int SongsCount { get; init; }

    /// <summary>
    ///     The ones among them that would gain the target's album artist, for not having it yet.
    /// </summary>
    public required int SongsGainingArtistCount { get; init; }

    /// <summary>
    ///     The name of the target's album artist.
    /// </summary>
    public required string TargetArtistName { get; init; }
}
