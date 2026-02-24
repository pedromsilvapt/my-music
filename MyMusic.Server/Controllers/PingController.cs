using Microsoft.AspNetCore.Mvc;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("ping")]
public class PingController : ControllerBase
{
    [HttpGet(Name = "Ping")]
    public IActionResult Ping() => Ok("pong");
}