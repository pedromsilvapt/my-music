using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(SongId), nameof(AuditRuleId), IsUnique = true)]
public class AuditNonConformity
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    public long AuditRuleId { get; set; }

    public bool HasWaiver { get; set; }

    [MaxLength(500)] public string? WaiverReason { get; set; }

    public User Owner { get; set; } = null!;
    public long OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}