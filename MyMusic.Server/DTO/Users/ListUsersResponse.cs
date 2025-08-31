using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Users;

public record ListUsersResponse
{
    public required IEnumerable<User> Users { get; set; }

    public record User
    {
        public required long Id { get; set; }
        public required string Username { get; set; }
        public required string Name { get; set; }

        public static User FromEntity(Entities.User user)
        {
            return new User
            {
                Id = user.Id,
                Name = user.Name,
                Username = user.Username,
            };
        }
    }
}