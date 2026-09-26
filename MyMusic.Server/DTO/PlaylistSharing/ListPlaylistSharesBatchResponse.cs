using MyMusic.Common.Services.Playlists;

namespace MyMusic.Server.DTO.PlaylistSharing;

public record ListPlaylistSharesBatchResponse
{
    public required List<PlaylistShareBatchItem> Shares { get; set; }
}

public record PlaylistShareBatchItem
{
    public required long Id { get; set; }
    public required long PlaylistId { get; set; }
    public required long UserId { get; set; }
    public required string Username { get; set; }
    public required DateTime CreatedAt { get; set; }

    public static PlaylistShareBatchItem FromDto(PlaylistShareDto dto) =>
        new()
        {
            Id = dto.Id,
            PlaylistId = dto.PlaylistId,
            UserId = dto.UserId,
            Username = dto.Username,
            CreatedAt = dto.CreatedAt,
        };
}
