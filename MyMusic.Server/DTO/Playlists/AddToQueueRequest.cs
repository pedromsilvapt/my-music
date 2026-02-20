namespace MyMusic.Server.DTO.Playlists;

public record AddToQueueRequest
{
    public required List<long> SongIds { get; init; }
    public AddToQueuePosition Position { get; init; } = AddToQueuePosition.Last;
}

public enum AddToQueuePosition
{
    Now = 0,
    Next = 1,
    Last = 2,
}