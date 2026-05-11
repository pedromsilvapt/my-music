namespace MyMusic.Server.DTO.Songs;

public record BatchDeleteSongsResponse
{
    public required int DeletedCount { get; set; }
}
