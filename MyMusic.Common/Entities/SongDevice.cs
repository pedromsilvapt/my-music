using System.ComponentModel.DataAnnotations;
using EntityFrameworkCore.Projectables;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(DeviceId), nameof(DevicePath), IsUnique = true)]
public class SongDevice
{
    public long Id { get; set; }

    public Song? Song { get; set; }
    public long? SongId { get; set; }

    public Device Device { get; set; } = null!;
    public long DeviceId { get; set; }

    [MaxLength(1024)] public required string DevicePath { get; set; }

    /// <summary>
    /// A path the user typed for the song on the device, not yet applied there. The next sync creates the
    /// file at (or renames it to) this path instead of the naming template's, and clears it.
    /// </summary>
    [MaxLength(1024)] public string? RequestedPath { get; set; }

    public SongSyncAction? SyncAction { get; set; }

    [MaxLength(2048)]
    public string? SyncActionReason { get; set; }

    public DateTime AddedAt { get; set; }

    public DateTime? LastSyncedModifiedAt { get; set; }

    /// <summary>
    /// How many times the song is placed on this device (a song can sit at several paths of the same device).
    /// Copies waiting to be removed by the next sync do not count.
    /// </summary>
    [Projectable]
    public int Copies => Song!.Devices.Count(d => d.DeviceId == DeviceId && d.SyncAction != SongSyncAction.Remove);
}

public enum SongSyncAction
{
    Download,
    Upload,
    Remove,
}