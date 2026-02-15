namespace MyMusic.Server.DTO.Playlists;

public record UpdatePlaylistRequest
{
    public required string Name { get; init; }
}