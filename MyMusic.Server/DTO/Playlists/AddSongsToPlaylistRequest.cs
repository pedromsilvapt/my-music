namespace MyMusic.Server.DTO.Playlists;

public record AddSongsToPlaylistRequest
{
    public required List<long> SongIds { get; init; }
}