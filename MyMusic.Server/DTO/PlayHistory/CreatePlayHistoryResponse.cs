namespace MyMusic.Server.DTO.PlayHistory;

public record CreatePlayHistoryResponse
{
    public required bool Created { get; init; }
    public required long Id { get; init; }
    public required int SongPlayCount { get; init; }
}