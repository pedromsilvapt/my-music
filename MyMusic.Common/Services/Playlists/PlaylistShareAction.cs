namespace MyMusic.Common.Services.Playlists;

/// <summary>
/// A single share action applied to all playlists in a batch manage-shares request.
/// Mirrors the <c>PlaylistSongAction</c> pattern from the Playlists DTO folder.
/// </summary>
public record PlaylistShareAction
{
    public required long UserId { get; init; }

    public required PlaylistShareActionType Action { get; init; }
}

/// <summary>
/// The type of a <see cref="PlaylistShareAction"/>.
/// </summary>
public enum PlaylistShareActionType
{
    Add,
    Remove,
}
