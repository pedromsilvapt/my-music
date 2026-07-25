using System.IO.Abstractions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common;
using MyMusic.Common.Entities;
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
    MusicDbContext context,
    IFileSystem fileSystem,
    ISyncStartService syncStartService,
    ISyncCompleteService syncCompleteService,
    ISyncCancelService syncCancelService,
    ISyncCommitService syncCommitService,
    ISyncSessionLookupService sessionLookup) : ControllerBase
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

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/cancel")]
    public async Task<ActionResult<SyncCancelResponse>> CancelSync(long deviceId, long sessionId,
        CancellationToken cancellationToken)
    {
        var result = await syncCancelService.CancelAsync(deviceId, sessionId, currentUser.Id, cancellationToken);
        if (result == null) return NotFound();

        return new SyncCancelResponse
        {
            SessionId = sessionId,
            StagingDirectoryDeleted = result.StagingDirectoryDeleted,
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/commit")]
    public async Task<ActionResult<SyncCommitResponse>> CommitSync(long deviceId, long sessionId,
        [FromBody] SyncCommitRequest? request, CancellationToken cancellationToken)
    {
        var session = await sessionLookup.FindSessionAsync(context, sessionId, deviceId, currentUser.Id, cancellationToken);
        if (session == null) return NotFound();

        if (session.Status != SyncSessionStatus.InProgress && session.Status != SyncSessionStatus.Committed)
        {
            throw new Exception($"Sync session {sessionId} cannot be committed (status: {session.Status})");
        }

        if (session.Status == SyncSessionStatus.Committed)
        {
            var existingRecords = await context.DeviceSyncSessionRecords
                .Where(r => r.SessionId == sessionId)
                .ToListAsync(cancellationToken);

            return SyncCommitResponseMapper.Map(existingRecords, session.CompletedAt ?? DateTime.UtcNow);
        }

        var direction = request?.Direction?.ToLowerInvariant() ?? "both";

        var result = await syncCommitService.CommitAsync(context, sessionId, deviceId, session.IsDryRun, direction, cancellationToken);

        session.Status = SyncSessionStatus.Committed;
        session.CompletedAt = DateTime.UtcNow;

        StagingDirectoryCleanupService.DeleteStagingDirectory(fileSystem, session.RepositoryPath, session.Id, logger);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Committed sync session {SessionId} for device {DeviceId}", sessionId, deviceId);

        return SyncCommitResponseMapper.Map(result, session.CompletedAt.Value);
    }
}