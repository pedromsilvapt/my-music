using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(SessionId), nameof(FilePath), IsUnique = true)]
public class DeviceSyncSessionRecord
{
    public long Id { get; set; }

    public DeviceSyncSession Session { get; set; } = null!;
    public long SessionId { get; set; }

    [MaxLength(1024)] public required string FilePath { get; set; }

    public long? SongId { get; set; }

    public SyncRecordAction Action { get; set; }

    public SyncRecordSource Source { get; set; }

    [MaxLength(2048)] public string? ErrorMessage { get; set; }

    [MaxLength(2048)] public string? Reason { get; set; }

    public DateTime ProcessedAt { get; set; }
}

public enum SyncRecordAction
{
    Created,
    Updated,
    Skipped,
    Downloaded,
    Removed,
    Error,
}

public enum SyncRecordSource
{
    Device,
    Server,
}