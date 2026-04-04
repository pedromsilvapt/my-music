using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

[Index(nameof(OwnerId), nameof(SourceId), nameof(Query), IsUnique = true)]
public class WishlistItem
{
    public long Id { get; set; }

    public required User Owner { get; set; }
    public long OwnerId { get; set; }

    public Source Source { get; set; } = null!;
    public long SourceId { get; set; }

    [MaxLength(512)] public required string Query { get; set; }

    [MaxLength(128)] public required string Hash { get; set; }

    public WishlistItemStatus Status { get; set; }

    public int ContinuousFailedCount { get; set; }

    [MaxLength(1024)] public string? LastErrorMessage { get; set; }

    public required DateTime CreatedAt { get; set; }

    public required DateTime UpdatedAt { get; set; }
}