namespace MyMusic.Server.DTO.PlayHistory;

public record CreatePlayHistoryRequest
{
    public required long SongId { get; init; }
    public required string ClientId { get; init; }
    public long? DeviceId { get; init; }
}