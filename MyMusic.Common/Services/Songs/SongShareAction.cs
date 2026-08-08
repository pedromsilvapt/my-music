namespace MyMusic.Common.Services;

/// <summary>
/// A single share action applied to all songs in a batch manage-shares request.
/// Mirrors the <c>PlaylistSongAction</c> pattern from the Playlists DTO folder.
/// </summary>
public record SongShareAction
{
    public required long UserId { get; init; }

    public required SongShareActionType Action { get; init; }
}

/// <summary>
/// The type of a <see cref="SongShareAction"/>.
/// </summary>
public enum SongShareActionType
{
    Add,
    Remove,
}