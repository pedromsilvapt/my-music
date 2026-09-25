namespace MyMusic.Common.Entities;

public class DeviceSyncSession
{
    public long Id { get; set; }

    public Device Device { get; set; } = null!;
    public long DeviceId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public SyncSessionStatus Status { get; set; }

    public bool IsDryRun { get; set; }

    /// <summary>
    /// Direction of the sync, chosen by the client when the session starts. Every step of the
    /// session (check, resolve-conflicts, commit) reads it from here so records are always
    /// produced under a single, consistent direction.
    /// </summary>
    public SyncDirection Direction { get; set; } = SyncDirection.Both;

    public string? RepositoryPath { get; set; }

    public List<DeviceSyncSessionRecord> Records { get; set; } = [];
}

public enum SyncSessionStatus
{
    InProgress,
    Committed,
    Completed,
    Cancelled,
}

/// <summary>
/// Direction of a sync session. See docs/development/sync.md, "Sync Direction".
/// </summary>
public enum SyncDirection
{
    /// <summary>Push and pull in the same session.</summary>
    Both,

    /// <summary>Push only: the device is the source of truth for this session.</summary>
    Up,

    /// <summary>Pull only: the server is the source of truth for this session.</summary>
    Down,
}
