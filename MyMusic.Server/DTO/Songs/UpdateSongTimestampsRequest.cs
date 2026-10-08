namespace MyMusic.Server.DTO.Songs;

public record UpdateSongTimestampsRequest
{
    /// <summary>When the song was first created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the song last changed.</summary>
    public required DateTime ModifiedAt { get; init; }

    /// <summary>When the song was added to the database; null clears it.</summary>
    public DateTime? AddedAt { get; init; }

    /// <summary>When the song's file last changed; null clears it.</summary>
    public DateTime? FileModifiedAt { get; init; }
}
