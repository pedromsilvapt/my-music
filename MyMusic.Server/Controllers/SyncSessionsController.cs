using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.DTO.Sync;

namespace MyMusic.Server.Controllers;

/// <summary>
/// Sync session lifecycle endpoints (list, records, filters, delete, prune). Lives under the
/// <c>devices</c> route prefix so session endpoints keep their existing paths
/// (<c>/devices/{deviceId}/sessions</c>, ...). Extracted from <see cref="DevicesController"/>
/// as part of the controllers refactor (Phase 5+).
/// </summary>
[ApiController]
[Route("devices")]
public class SyncSessionsController(
    ILogger<SyncSessionsController> logger,
    ICurrentUser currentUser,
    ISyncSessionListService sessionListService) : ControllerBase
{
    [HttpGet("{deviceId:long}/sessions")]
    public async Task<ActionResult<ListSyncSessionsResponse>> ListSessions(
        long deviceId,
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        var result = await sessionListService.ListAsync(deviceId, currentUser.Id, count, cancellationToken);
        if (result == null) return NotFound();

        return new ListSyncSessionsResponse
        {
            Sessions = result.Sessions.Select(SyncSessionItem.FromEntity).ToList(),
        };
    }
}