namespace MyMusic.Server.DTO.Devices;

public record PruneSessionsResponse
{
    public required int DeletedCount { get; init; }
}