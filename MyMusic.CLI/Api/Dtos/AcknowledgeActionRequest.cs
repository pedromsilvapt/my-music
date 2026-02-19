namespace MyMusic.CLI.Api.Dtos;

public record AcknowledgeActionRequest
{
    public required long SongId { get; init; }
}

public record AcknowledgeActionResponse
{
    public required bool Success { get; init; }
}