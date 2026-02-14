using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class PurchasedSong
{
    public long Id { get; set; }

    public Source Source { get; set; } = null!;
    public long SourceId { get; set; }

    public User User { get; set; } = null!;
    public long UserId { get; set; }

    [MaxLength(256)] public required string ExternalId { get; set; }

    public string? Cover { get; set; }

    [MaxLength(256)] public required string Title { get; set; }

    [MaxLength(256)] public required string SubTitle { get; set; }

    public required PurchasedSongStatus Status { get; set; } = PurchasedSongStatus.Queued;

    public required int Progress { get; set; }

    public long? SongId { get; set; } = null;
    public Song? Song { get; set; } = null;

    public string? ErrorMessage { get; set; }

    public required DateTime CreatedAt { get; set; }
}

public enum PurchasedSongStatus
{
    Queued = 0,
    Acquiring = 1,
    Completed = 2,
    Failed = 3,
}