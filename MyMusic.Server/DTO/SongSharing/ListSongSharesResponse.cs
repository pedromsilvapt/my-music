using MyMusic.Common.Services;

namespace MyMusic.Server.DTO.SongSharing;

public record ListSongSharesResponse
{
    public required List<SongShareItem> Shares { get; set; }
}

public record SongShareItem
{
    public required long Id { get; set; }
    public required long UserId { get; set; }
    public required string Username { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static SongShareItem FromDto(SongShareDto dto) =>
        new()
        {
            Id = dto.Id,
            UserId = dto.UserId,
            Username = dto.Username,
            CreatedAt = dto.CreatedAt,
        };
}