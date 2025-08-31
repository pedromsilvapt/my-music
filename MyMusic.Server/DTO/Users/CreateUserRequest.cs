namespace MyMusic.Server.DTO.Users;

public record CreateUserRequest
{
    public required UserData User { get; set; }

    public record UserData
    {
        public required string Username { get; set; }
        public required string Name { get; set; }
    }
}