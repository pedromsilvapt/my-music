namespace MyMusic.Server.DTO.Users;

public record CreateUserResponse
{
    public ListUsersResponse.User User { get; set; }
}