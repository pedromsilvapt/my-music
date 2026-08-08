using MyMusic.Common.Services;

namespace MyMusic.Server.DTO.SongSharing;

public record ListSongSharesBatchResponse
{
    public required List<SongShareBatchItem> Shares { get; set; }
}

public record SongShareBatchItem
{
    public required long Id { get; set; }
    public required long SongId { get; set; }
    public required long UserId { get; set; }
    public required string Username { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static SongShareBatchItem FromDto(SongShareDto dto) =>
        new()
        {
            Id = dto.Id,
            SongId = dto.SongId,
            UserId = dto.UserId,
            Username = dto.Username,
            CreatedAt = dto.CreatedAt,
        };
}