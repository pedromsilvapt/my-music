namespace MyMusic.Server.DTO.Playlists;

public record SetSkipNextPlaybackRequest
{
    public required bool SkipNextPlayback { get; init; }
}
