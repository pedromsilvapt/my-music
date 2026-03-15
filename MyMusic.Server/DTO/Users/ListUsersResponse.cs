using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Users;

public record ListUsersResponse
{
    public required IEnumerable<ListUsersItem> Users { get; set; }
}

public record ListUsersItem
{
    public required long Id { get; set; }
    public required string Username { get; set; }
    public required string Name { get; set; }
    public required string ColorScheme { get; set; }
    public required double Volume { get; set; }
    public required bool IsMuted { get; set; }

    public static ListUsersItem FromEntity(Entities.User user) =>
        new()
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            ColorScheme = user.ColorScheme,
            Volume = user.Volume,
            IsMuted = user.IsMuted,
        };
}