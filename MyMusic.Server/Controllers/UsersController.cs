using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Server.DTO.Users;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class UsersController(ILogger<UsersController> logger) : ControllerBase
{
    private readonly ILogger<UsersController> _logger = logger;

    [HttpGet(Name = "ListUsers")]
    public async Task<ListUsersResponse> Get(MusicDbContext context, CancellationToken cancellationToken)
    {
        var users = await context.Users.ToListAsync(cancellationToken);

        return new ListUsersResponse
        {
            Users = users.Select(ListUsersItem.FromEntity).ToList(),
        };
    }

    [HttpPost(Name = "CreateUser")]
    public async Task<CreateUserResponse> Create(
        [FromServices] MusicDbContext db,
        [FromBody] CreateUserRequest body,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Name = body.User.Name,
            Username = body.User.Username,
        };

        await db.AddAsync(user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateUserResponse
        {
            User = CreateUserItem.FromEntity(user),
        };
    }
}