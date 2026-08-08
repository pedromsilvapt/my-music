using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

/// <summary>
/// Represents a share of a <see cref="Song"/> with a recipient <see cref="User"/>.
/// The sharer is derivable from <see cref="Song.OwnerId"/>, so this entity intentionally
/// does NOT carry an <c>OwnerId</c>/<c>SharerId</c> field. This is a deliberate deviation
/// from the repo's "every entity has OwnerId" convention — do not "auto-correct" it.
/// </summary>
[Index(nameof(SongId), nameof(UserId), IsUnique = true)]
public class SongSharing
{
    public long Id { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    public User User { get; set; } = null!;
    public long UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }
}