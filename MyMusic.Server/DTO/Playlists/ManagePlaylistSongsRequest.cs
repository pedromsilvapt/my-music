namespace MyMusic.Server.DTO.Playlists;

public record ManagePlaylistSongsRequest
{
    public required List<long> SongIds { get; init; }
    public required List<PlaylistSongAction> Playlists { get; init; }
}

public record PlaylistSongAction
{
    public required long PlaylistId { get; init; }
    public required PlaylistAction Action { get; init; }
}

public enum PlaylistAction
{
    Add,
    Remove
}