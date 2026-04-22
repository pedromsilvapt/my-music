namespace MyMusic.Server.DTO.Playlists;

public record BatchSetSkipNextPlaybackRequest
{
    public required long PlaylistId { get; init; }
    public required List<long> SongIds { get; init; }
    public required bool SkipNextPlayback { get; init; }
}
