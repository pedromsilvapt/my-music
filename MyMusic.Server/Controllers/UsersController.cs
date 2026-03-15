using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Users;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("users")]
public class UsersController(
    ILogger<UsersController> logger,
    ICurrentUser currentUser) : ControllerBase
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

    [HttpGet("me", Name = "GetCurrentUser")]
    public async Task<ActionResult<GetUserResponse>> GetCurrentUser(
        [FromServices] MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([currentUser.Id], cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new GetUserResponse
        {
            User = GetUserItem.FromEntity(user),
        });
    }

    [HttpPatch("me", Name = "UpdateCurrentUser")]
    public async Task<ActionResult<GetUserResponse>> UpdateCurrentUser(
        [FromServices] MusicDbContext db,
        [FromBody] UpdateUserRequest body,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([currentUser.Id], cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        if (body.ColorScheme != null)
        {
            var validSchemes = new[] { "light", "dark", "auto" };
            if (!validSchemes.Contains(body.ColorScheme))
            {
                return BadRequest("Invalid colorScheme. Must be 'light', 'dark', or 'auto'.");
            }

            user.ColorScheme = body.ColorScheme;
        }

        if (body.Volume != null)
        {
            if (body.Volume < 0 || body.Volume > 1)
            {
                return BadRequest("Invalid volume. Must be between 0 and 1.");
            }

            user.Volume = body.Volume.Value;
        }

        if (body.IsMuted != null)
        {
            user.IsMuted = body.IsMuted.Value;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new GetUserResponse
        {
            User = GetUserItem.FromEntity(user),
        });
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