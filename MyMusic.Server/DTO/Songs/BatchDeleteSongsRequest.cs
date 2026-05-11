namespace MyMusic.Server.DTO.Songs;

public record BatchDeleteSongsRequest
{
    public required List<long> SongIds { get; set; }
}
