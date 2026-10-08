using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Songs;

public record UpdateSongTimestampsResponse
{
    public required DateTime CreatedAt { get; init; }
    public required DateTime ModifiedAt { get; init; }
    public DateTime? AddedAt { get; init; }
    public DateTime? FileModifiedAt { get; init; }

    public static UpdateSongTimestampsResponse FromEntity(Entities.Song song) =>
        new()
        {
            CreatedAt = song.CreatedAt,
            ModifiedAt = song.ModifiedAt,
            AddedAt = song.AddedAt,
            FileModifiedAt = song.FileModifiedAt,
        };
}
