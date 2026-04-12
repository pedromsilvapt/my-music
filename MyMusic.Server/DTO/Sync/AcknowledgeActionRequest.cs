namespace MyMusic.Server.DTO.Sync;

public record AcknowledgeActionRequest
{
    public required string DevicePath { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

public record AcknowledgeActionResponse
{
    public required bool Success { get; init; }
}