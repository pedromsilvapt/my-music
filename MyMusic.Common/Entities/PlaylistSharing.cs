using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

/// <summary>
/// Represents a share of a <see cref="Playlist"/> with a recipient <see cref="User"/>.
/// The sharer is derivable from <see cref="Playlist.OwnerId"/>, so this entity intentionally
/// does NOT carry an <c>OwnerId</c>/<c>SharerId</c> field. This is a deliberate deviation
/// from the repo's "every entity has OwnerId" convention — do not "auto-correct" it.
/// Songs are shared implicitly: see <see cref="Song.IsSharedWith"/>.
/// </summary>
[Index(nameof(PlaylistId), nameof(UserId), IsUnique = true)]
public class PlaylistSharing
{
    public long Id { get; set; }

    public Playlist Playlist { get; set; } = null!;
    public long PlaylistId { get; set; }

    public User User { get; set; } = null!;
    public long UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }
}
