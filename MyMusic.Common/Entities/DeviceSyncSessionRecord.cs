using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

public class DeviceSyncSessionRecord
{
    public long Id { get; set; }

    public DeviceSyncSession Session { get; set; } = null!;
    public long SessionId { get; set; }

    [MaxLength(1024)] public required string FilePath { get; set; }

    public Song? Song { get; set; }
    public long? SongId { get; set; }

    public SyncRecordAction Action { get; set; }

    public JsonElement? Data { get; set; }

    [MaxLength(2048)] public string? Reason { get; set; }

    public long? ResolvesConflictRecordId { get; set; }

    public DeviceSyncSessionRecord? ResolvesConflictRecord { get; set; }

    public bool Acknowledged { get; set; } = false;

    public DateTime ProcessedAt { get; set; }
}

public enum SyncRecordAction
{
    CreateRemote,
    UpdateRemote,
    CreateLocal,
    UpdateLocal,
    Delete,
    Link,
    Unlink,
    Rename,
    Skipped,
    Conflict,
    UpdateTimestamp,
    Error,
}