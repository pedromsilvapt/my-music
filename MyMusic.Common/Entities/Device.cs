using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(OwnerId), nameof(Name), IsUnique = true)]
public class Device
{
    public long Id { get; set; }

    [MaxLength(256)] public required string Name { get; set; }

    public required User Owner { get; set; }
    public long OwnerId { get; set; }

    [MaxLength(50)] public string? Icon { get; set; }

    [MaxLength(20)] public string? Color { get; set; }

    [MaxLength(512)] public string? NamingTemplate { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public List<SongDevice> Songs { get; set; } = [];
}