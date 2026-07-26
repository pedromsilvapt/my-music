using System.IO.Abstractions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.DTO.Devices;
using MyMusic.Server.DTO.Sync;

namespace MyMusic.Server.Controllers;

/// <summary>
/// Sync workflow endpoints (start/complete/cancel/commit/check/resolve/upload/error/acknowledge/
/// pending-actions/device-songs). Lives under the <c>devices</c> route prefix so the workflow
/// endpoints keep their existing paths (<c>/devices/{deviceId}/sync/start</c>, ...).
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
    ISyncPendingActionsService syncPendingActionsService,
    ISyncDeviceSongsService syncDeviceSongsService,
    ISyncCheckService syncCheckService,
    ISyncResolveConflictsService syncResolveConflictsService,
    ISyncReportErrorService syncReportErrorService,
    ISyncAcknowledgeService syncAcknowledgeService,
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

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/pending-actions")]
    public async Task<ActionResult<CreatePendingActionsResponse>> CreatePendingActions(long deviceId, long sessionId, CancellationToken cancellationToken)
    {
        var result = await syncPendingActionsService.CreateAsync(deviceId, sessionId, currentUser.Id, cancellationToken);
        if (result == null) return NotFound();

        return new CreatePendingActionsResponse
        {
            Records = result.Records.Select(r => SyncRecordResponseItem.FromEntity(r)).ToList(),
        };
    }

    [HttpGet("{deviceId:long}/sync/songs")]
    public async Task<ActionResult<GetDeviceSongsResponse>> GetDeviceSongs(long deviceId, CancellationToken cancellationToken)
    {
        var result = await syncDeviceSongsService.GetAsync(deviceId, currentUser.Id, cancellationToken);
        if (result == null) return NotFound();

        return new GetDeviceSongsResponse
        {
            Songs = result.Songs.Select(s => new DeviceSongItem
            {
                SongId = s.SongId,
                Path = s.Path,
                Action = s.Action,
            }).ToList(),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/check")]
    public async Task<ActionResult<SyncCheckResponse>> CheckSync(long deviceId, long sessionId, [FromBody] SyncCheckRequest request,
        CancellationToken cancellationToken)
    {
        var result = await syncCheckService.CheckAsync(
            deviceId,
            sessionId,
            currentUser.Id,
            new SyncCheckInput
            {
                Files = request.Files.Select(f => new SyncCheckFileInfo
                {
                    Path = f.Path,
                    ModifiedAt = f.ModifiedAt,
                    CreatedAt = f.CreatedAt,
                }).ToList(),
                Force = request.Force,
            },
            cancellationToken);
        if (result == null) return NotFound();

        return new SyncCheckResponse
        {
            Records = result.Records.Select(r => SyncRecordResponseItem.FromEntity(r)).ToList(),
            Counts = SyncActionCounts.FromRecords(result.Records.Where(r => r.Action != SyncRecordAction.UpdateLocal && r.Action != SyncRecordAction.Conflict)),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/resolve-conflicts")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<SyncResolveConflictsResponse>> ResolveConflicts(
        long deviceId,
        long sessionId,
        [FromBody] SyncResolveConflictsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await syncResolveConflictsService.ResolveAsync(
            deviceId,
            sessionId,
            currentUser.Id,
            new SyncResolveConflictsInput
            {
                Conflicts = request.Conflicts.Select(c => new SyncResolveConflictItem
                {
                    Path = c.Path,
                    SongId = c.SongId,
                    FileContentBase64 = c.FileContentBase64,
                    LocalModifiedAt = c.LocalModifiedAt,
                }).ToList(),
                PotentialUpdates = request.PotentialUpdates.Select(u => new SyncResolvePotentialUpdateItem
                {
                    Path = u.Path,
                    SongId = u.SongId,
                    FileContentBase64 = u.FileContentBase64,
                    LocalModifiedAt = u.LocalModifiedAt,
                    LastSyncedAt = u.LastSyncedAt,
                }).ToList(),
            },
            cancellationToken);
        if (result == null) return NotFound();

        return new SyncResolveConflictsResponse
        {
            Records = result.Records.Select(r => SyncRecordResponseItem.FromEntity(r)).ToList(),
            Counts = SyncActionCounts.FromRecords(result.Records),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/error")]
    public async Task<ActionResult<ReportSyncErrorResponse>> ReportSyncError(long deviceId, long sessionId,
        [FromBody] ReportSyncErrorRequest request, CancellationToken cancellationToken)
    {
        var result = await syncReportErrorService.ReportErrorAsync(
            deviceId,
            sessionId,
            currentUser.Id,
            new SyncReportErrorInput
            {
                FilePath = request.FilePath,
                ErrorMessage = request.ErrorMessage,
                SongId = request.SongId,
            },
            cancellationToken);
        if (!result.Found)
        {
            return result.Failure == SyncReportErrorFailure.SessionNotFound
                ? NotFound($"Sync session not found with id {sessionId}")
                : NotFound();
        }

        return new ReportSyncErrorResponse
        {
            Counts = SyncActionCounts.FromAction(SyncRecordAction.Error),
        };
    }

    [HttpPost("{deviceId:long}/sync/{sessionId:long}/acknowledge")]
    public async Task<ActionResult<AcknowledgeActionResponse>> AcknowledgeAction(long deviceId, long sessionId,
        [FromBody] AcknowledgeActionRequest request, CancellationToken cancellationToken)
    {
        var result = await syncAcknowledgeService.AcknowledgeAsync(
            deviceId,
            currentUser.Id,
            new SyncAcknowledgeInput
            {
                RecordIds = request.RecordIds,
                ModifiedAt = request.ModifiedAt,
            },
            cancellationToken);
        if (!result.Found) return NotFound();
        if (result.BadRequest) return BadRequest("RecordIds is required");

        return new AcknowledgeActionResponse
        {
            Success = true,
            Counts = SyncActionCounts.FromRecords(result.Records),
        };
    }
}