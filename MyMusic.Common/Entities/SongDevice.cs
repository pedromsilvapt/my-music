using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(DeviceId), nameof(DevicePath), IsUnique = true)]
public class SongDevice
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    public Device Device { get; set; } = null!;
    public long DeviceId { get; set; }

    [MaxLength(1024)]
    public required string DevicePath { get; set; }
    
    public SongSyncAction? SyncAction { get; set; }

    public DateTime AddedAt { get; set; }
}

public enum SongSyncAction
{
    Download,
    Upload,
    Remove,
}
