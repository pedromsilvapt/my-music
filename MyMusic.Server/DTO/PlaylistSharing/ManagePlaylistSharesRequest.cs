namespace MyMusic.Server.DTO.PlaylistSharing;

public record ManagePlaylistSharesRequest
{
    public required long[] PlaylistIds { get; init; }
    public required List<PlaylistShareActionItem> Shares { get; init; }
}

public record PlaylistShareActionItem
{
    public required long UserId { get; init; }

    /// <summary>"Add" or "Remove".</summary>
    public required string Action { get; init; }
}
