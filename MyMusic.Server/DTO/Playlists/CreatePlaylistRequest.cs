namespace MyMusic.Server.DTO.Playlists;

public record CreatePlaylistRequest
{
    public required string Name { get; init; }
}