using MyMusic.Common.Services;

namespace MyMusic.Server.DTO.SongSharing;

public record ListSharersResponse
{
    public required List<SongSharerItem> Sharers { get; set; }
}

public record SongSharerItem
{
    public required long Id { get; set; }
    public required string Username { get; set; }
    public required string Name { get; set; }

    public static SongSharerItem FromDto(SongSharerDto dto) =>
        new()
        {
            Id = dto.Id,
            Username = dto.Username,
            Name = dto.Name,
        };
}