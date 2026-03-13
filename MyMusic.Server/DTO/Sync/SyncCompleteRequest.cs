namespace MyMusic.Server.DTO.Sync;

public record SyncCompleteRequest
{
    public string? Direction { get; init; }
}