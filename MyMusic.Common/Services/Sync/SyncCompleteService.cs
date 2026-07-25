using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Result of a sync session complete operation. The controller maps this to
/// <c>SyncCompleteResponse</c>.
/// </summary>
public record SyncCompleteResult
{
    public required int CreateRemoteCount { get; init; }

    public required int UpdateRemoteCount { get; init; }

    public required int SkippedCount { get; init; }

    public required int CreateLocalCount { get; init; }

    public required int UpdateLocalCount { get; init; }

    public required int DeleteLocalCount { get; init; }

    public required int LinkCount { get; init; }

    public required int UnlinkCount { get; init; }

    public required int RenameCount { get; init; }

    public required int ConflictCount { get; init; }

    public required int UpdateTimestampCount { get; init; }

    public required int ErrorCount { get; init; }
}

/// <summary>
/// Failure reasons returned by <see cref="ISyncCompleteService.CompleteAsync"/>. The controller
/// maps each value to the appropriate HTTP response (NotFound / thrown exception) without
/// leaking exceptions.
/// </summary>
public enum SyncCompleteFailure
{
    NotFound,
}

/// <summary>
/// Completes a <see cref="DeviceSyncSession"/> previously committed for a device owned by the
/// current user, updating the device's <see cref="Device.LastSyncAt"/> (non-dry-run only) and
/// aggregating the session's records into per-action counts. Extracted from
/// DevicesController.CompleteSync so the controller stays thin. Reuses
/// <see cref="ISyncSessionLookupService"/> for the session identity check.
/// </summary>
public interface ISyncCompleteService
{
    /// <summary>
    /// Completes the session <paramref name="sessionId"/> scoped to <paramref name="deviceId"/>
    /// owned by <paramref name="ownerId"/>. Throws when the session is still in progress or is
    /// not in the <see cref="SyncSessionStatus.Committed"/> state, mirroring the previous
    /// controller behavior. Returns <c>null</c> when no such session exists (NotFound).
    /// </summary>
    Task<SyncCompleteResult?> CompleteAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation of <see cref="ISyncCompleteService"/>.
/// </summary>
public class SyncCompleteService(
    MusicDbContext db,
    ISyncSessionLookupService sessionLookup,
    ILogger<SyncCompleteService> logger) : ISyncCompleteService
{
    /// <inheritdoc />
    public async Task<SyncCompleteResult?> CompleteAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken)
    {
        var session = await sessionLookup.FindSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (session == null) return null;

        if (session.Status == SyncSessionStatus.InProgress)
        {
            throw new Exception($"Sync session {sessionId} must be committed before completion (status: {session.Status})");
        }

        if (session.Status != SyncSessionStatus.Committed)
        {
            throw new Exception($"Sync session {sessionId} cannot be completed (status: {session.Status})");
        }

        session.CompletedAt = DateTime.UtcNow;
        session.Status = SyncSessionStatus.Completed;

        if (!session.IsDryRun)
        {
            var device = await db.Devices.FindAsync([deviceId], cancellationToken);
            if (device != null)
            {
                device.LastSyncAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var records = await db.DeviceSyncSessionRecords
            .Where(r => r.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Completed sync session {SessionId}: Created={Created}, Updated={Updated}, Skipped={Skipped}, Downloaded={Downloaded}, Removed={Removed}, Error={Error}",
            sessionId,
            records.Count(r => r.Action == SyncRecordAction.CreateRemote),
            records.Count(r => r.Action == SyncRecordAction.UpdateRemote),
            records.Count(r => r.Action == SyncRecordAction.Skipped),
            records.Count(r => r.Action == SyncRecordAction.CreateLocal || r.Action == SyncRecordAction.UpdateLocal),
            records.Count(r => r.Action == SyncRecordAction.DeleteLocal || r.Action == SyncRecordAction.Unlink),
            records.Count(r => r.Action == SyncRecordAction.Error));

        return new SyncCompleteResult
        {
            CreateRemoteCount = records.Count(r => r.Action == SyncRecordAction.CreateRemote),
            UpdateRemoteCount = records.Count(r => r.Action == SyncRecordAction.UpdateRemote),
            SkippedCount = records.Count(r => r.Action == SyncRecordAction.Skipped),
            CreateLocalCount = records.Count(r => r.Action == SyncRecordAction.CreateLocal),
            UpdateLocalCount = records.Count(r => r.Action == SyncRecordAction.UpdateLocal),
            DeleteLocalCount = records.Count(r => r.Action == SyncRecordAction.DeleteLocal),
            LinkCount = records.Count(r => r.Action == SyncRecordAction.Link),
            UnlinkCount = records.Count(r => r.Action == SyncRecordAction.Unlink),
            RenameCount = records.Count(r => r.Action == SyncRecordAction.Rename),
            ConflictCount = records.Count(r => r.Action == SyncRecordAction.Conflict),
            UpdateTimestampCount = records.Count(r => r.Action == SyncRecordAction.UpdateTimestamp),
            ErrorCount = records.Count(r => r.Action == SyncRecordAction.Error),
        };
    }
}