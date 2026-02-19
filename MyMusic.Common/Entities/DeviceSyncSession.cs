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

    public List<DeviceSyncSessionRecord> Records { get; set; } = [];
}

public enum SyncSessionStatus
{
    InProgress,
    Completed,
    Cancelled,
}