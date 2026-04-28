namespace MyMusic.Server.DTO.Users;

public record DeleteUserResponse
{
    public required DeleteUserItem User { get; set; }
}

public record DeleteUserItem
{
    public required long Id { get; set; }
    public required string Username { get; set; }
    public required string Name { get; set; }
}
