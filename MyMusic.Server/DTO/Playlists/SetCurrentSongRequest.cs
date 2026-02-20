namespace MyMusic.Server.DTO.Playlists;

public record SetCurrentSongRequest
{
    public long? CurrentSongId { get; init; }
}