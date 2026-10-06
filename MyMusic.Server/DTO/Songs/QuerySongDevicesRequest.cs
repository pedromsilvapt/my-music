namespace MyMusic.Server.DTO.Songs;

public record QuerySongDevicesRequest
{
    public required List<long> SongIds { get; init; }
}
