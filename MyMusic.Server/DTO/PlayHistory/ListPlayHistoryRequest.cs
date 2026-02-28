namespace MyMusic.Server.DTO.PlayHistory;

public record ListPlayHistoryRequest
{
    public long? LastId { get; init; }
    public int Limit { get; init; } = 50;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public long? SongId { get; init; }
}