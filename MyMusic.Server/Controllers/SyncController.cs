using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.DTO.Sync;

namespace MyMusic.Server.Controllers;

/// <summary>
/// Sync workflow endpoints (start/complete/cancel/commit/check/resolve/upload/error/acknowledge/
/// pending-actions/device-songs). Lives under the <c>devices</c> route prefix so the workflow
/// endpoints keep their existing paths (<c>/devices/{deviceId}/sync/start</c>, ...). Extracted
/// from <see cref="DevicesController"/> as part of the controllers refactor (Phase 8+).
/// </summary>
[ApiController]
[Route("devices")]
public class SyncController(
    ILogger<SyncController> logger,
    ICurrentUser currentUser,
    ISyncStartService syncStartService,
    ISyncCompleteService syncCompleteService) : ControllerBase
{
    [HttpPost("{deviceId:long}/sync/start")]
    public async Task<ActionResult<SyncStartResponse>> StartSync(long deviceId, [FromBody] SyncStartRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await syncStartService.StartAsync(
            deviceId,
            currentUser.Id,
            new SyncStartInput
            {
                DryRun = request?.DryRun ?? false,
                RepositoryPath = request?.RepositoryPath,
                ScanErrors = request?.ScanErrors?
                    .Select(e => new SyncStartScanError { FilePath = e.FilePath, ErrorMessage = e.ErrorMessage })
                    .ToList(),
            },
            cancellationToken);
        if (result == null) return NotFound();

        return new SyncStartResponse { SessionId = result.SessionId };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/complete")]
    public async Task<ActionResult<SyncCompleteResponse>> CompleteSync(long deviceId, long sessionId,
        [FromBody] SyncCompleteRequest? request, CancellationToken cancellationToken)
    {
        var result = await syncCompleteService.CompleteAsync(deviceId, sessionId, currentUser.Id, cancellationToken);
        if (result == null) return NotFound();

        return new SyncCompleteResponse
        {
            CreateRemoteCount = result.CreateRemoteCount,
            UpdateRemoteCount = result.UpdateRemoteCount,
            SkippedCount = result.SkippedCount,
            CreateLocalCount = result.CreateLocalCount,
            UpdateLocalCount = result.UpdateLocalCount,
            DeleteLocalCount = result.DeleteLocalCount,
            LinkCount = result.LinkCount,
            UnlinkCount = result.UnlinkCount,
            RenameCount = result.RenameCount,
            ConflictCount = result.ConflictCount,
            UpdateTimestampCount = result.UpdateTimestampCount,
            ErrorCount = result.ErrorCount,
        };
    }
}