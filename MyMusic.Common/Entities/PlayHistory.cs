using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(OwnerId), nameof(ClientId), IsUnique = true)]
public class PlayHistory
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    public User Owner { get; set; } = null!;
    public long OwnerId { get; set; }

    public Device? Device { get; set; }
    public long? DeviceId { get; set; }

    [MaxLength(36)] public required string ClientId { get; set; }

    public DateTime PlayedAt { get; set; }
}