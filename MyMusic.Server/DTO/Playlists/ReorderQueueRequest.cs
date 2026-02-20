namespace MyMusic.Server.DTO.Playlists;

public record ReorderQueueRequest
{
    public required List<ReorderQueueItem> Reorders { get; init; }
}

public record ReorderQueueItem
{
    public required int FromIndex { get; init; }
    public required int ToIndex { get; init; }
}