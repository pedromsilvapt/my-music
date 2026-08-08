namespace MyMusic.Common.Services;

/// <summary>
/// DTO representing a user who has shared at least one song with the current user.
/// Returned by <see cref="ISongShareService.ListSharersAsync"/> and drives the client's
/// "shared with me" sharer sub-menu.
/// </summary>
public record SongSharerDto
{
    public long Id { get; init; }
    public required string Username { get; init; }
    public required string Name { get; init; }
}