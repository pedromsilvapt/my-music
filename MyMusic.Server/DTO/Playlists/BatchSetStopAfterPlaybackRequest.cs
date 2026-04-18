namespace MyMusic.Server.DTO.Playlists;

public record BatchSetStopAfterPlaybackRequest
{
    public required long PlaylistId { get; init; }
    public required List<long> SongIds { get; init; }
    public required bool StopAfterPlayback { get; init; }
}