namespace MyMusic.CLI.Api.Dtos;

public record SyncFileInfoItem
{
    public required string Path { get; init; }
    public required DateTime ModifiedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? Reason { get; init; }
}