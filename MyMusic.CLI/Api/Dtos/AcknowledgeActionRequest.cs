namespace MyMusic.CLI.Api.Dtos;

public record AcknowledgeActionRequest
{
    public required List<long> RecordIds { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

public record AcknowledgeActionResponse
{
    public required bool Success { get; init; }
    public required SyncActionCounts Counts { get; init; }
}