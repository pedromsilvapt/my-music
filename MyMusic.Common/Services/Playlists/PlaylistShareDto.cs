namespace MyMusic.Common.Services.Playlists;

/// <summary>
/// DTO representing a single <see cref="Entities.PlaylistSharing"/> row, returned by
/// <see cref="IPlaylistShareListService.ListSharesAsync"/>.
/// </summary>
public record PlaylistShareDto
{
    public long Id { get; init; }
    public long PlaylistId { get; init; }
    public long UserId { get; init; }
    public required string Username { get; init; }
    public DateTime CreatedAt { get; init; }
}
