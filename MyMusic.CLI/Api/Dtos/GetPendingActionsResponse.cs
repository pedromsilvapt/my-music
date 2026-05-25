namespace MyMusic.CLI.Api.Dtos;

public record CreatePendingActionsResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
}