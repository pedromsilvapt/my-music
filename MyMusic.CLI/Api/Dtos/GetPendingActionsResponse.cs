namespace MyMusic.CLI.Api.Dtos;

public record GetPendingActionsResponse
{
    public required List<PendingActionItem> Actions { get; init; }
}

public record PendingActionItem
{
    public required long SongId { get; init; }
    public required string Path { get; init; }
    public required string Action { get; init; }
}