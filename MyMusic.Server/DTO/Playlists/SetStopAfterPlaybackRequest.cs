namespace MyMusic.Server.DTO.Playlists;

public record SetStopAfterPlaybackRequest
{
    public required bool StopAfterPlayback { get; init; }
}