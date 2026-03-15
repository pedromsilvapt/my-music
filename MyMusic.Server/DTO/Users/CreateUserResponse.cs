using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Users;

public record CreateUserResponse
{
    public required CreateUserItem User { get; set; }
}

public record CreateUserItem : ListUsersItem
{
    public new static CreateUserItem FromEntity(Entities.User user) =>
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