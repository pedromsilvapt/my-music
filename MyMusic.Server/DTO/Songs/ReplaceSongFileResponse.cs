using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Songs;

public record ReplaceSongFileResponse
{
    public required long Id { get; init; }
    public required string RepositoryPath { get; init; }
    public required long Size { get; init; }

    /// <summary>The duration of the new audio, in seconds.</summary>
    public required double Duration { get; init; }

    public int? Bitrate { get; init; }

    public static ReplaceSongFileResponse FromEntity(Entities.Song song) =>
        new()
        {
            Id = song.Id,
            RepositoryPath = song.RepositoryPath,
            Size = song.Size,
            Duration = song.Duration.TotalSeconds,
            Bitrate = song.Bitrate,
        };
}
