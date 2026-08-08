namespace MyMusic.Common.Services;

/// <summary>
/// DTO representing a single <see cref="Entities.SongSharing"/> row, returned by
/// <see cref="ISongShareService.ListSharesAsync"/> and mapped by the controller into its
/// response DTO. Lives in Common because the service returns it and the Server DTO maps from it.
/// </summary>
public record SongShareDto
{
    public long Id { get; init; }
    public long SongId { get; init; }
    public long UserId { get; init; }
    public required string Username { get; init; }
    public DateTime CreatedAt { get; init; }
}