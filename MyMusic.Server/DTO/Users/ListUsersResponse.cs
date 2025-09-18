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

    public static ListUsersItem FromEntity(Entities.User user)
    {
        return new ListUsersItem
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
        };
    }
}