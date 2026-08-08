namespace MyMusic.Server.DTO.SongSharing;

public record ManageSongSharesRequest
{
    public required long[] SongIds { get; init; }
    public required List<SongShareActionItem> Shares { get; init; }
}

public record SongShareActionItem
{
    public required long UserId { get; init; }

    /// <summary>"Add" or "Remove".</summary>
    public required string Action { get; init; }
}