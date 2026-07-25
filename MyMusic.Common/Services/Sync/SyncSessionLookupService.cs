using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Result of <see cref="ISyncSessionLookupService.GetActiveSessionAsync"/>. The session is present
/// when <see cref="Found"/> is <c>true</c>; otherwise the caller should map <see cref="Failure"/>
/// to the appropriate HTTP response.
/// </summary>
public sealed class ActiveSessionResult
{
    public bool Found { get; init; }

    public DeviceSyncSession? Session { get; init; }

    public ActiveSessionFailure Failure { get; init; }

    public static ActiveSessionResult NotFound { get; } = new() { Found = false, Failure = ActiveSessionFailure.NotFound };

    public static ActiveSessionResult NotInProgress(long sessionId, SyncSessionStatus status) => new()
    {
        Found = false,
        Failure = ActiveSessionFailure.NotInProgress,
        NotInProgressSessionId = sessionId,
        NotInProgressStatus = status,
    };

    public static ActiveSessionResult Succeeded(DeviceSyncSession session) => new()
    {
        Found = true,
        Session = session,
    };

    public long? NotInProgressSessionId { get; init; }

    public SyncSessionStatus? NotInProgressStatus { get; init; }
}

public enum ActiveSessionFailure
{
    NotFound,
    NotInProgress,
}

/// <summary>
/// Shared lookup helpers for <see cref="DeviceSyncSession"/> entities scoped to a device owned by the current user.
/// </summary>
public interface ISyncSessionLookupService
{
    /// <summary>
    /// Finds a session for <paramref name="deviceId"/> owned by <paramref name="ownerId"/> by <paramref name="sessionId"/>.
    /// </summary>
    Task<DeviceSyncSession?> FindSessionAsync(MusicDbContext db, long sessionId, long deviceId, long ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the active (in-progress) session for <paramref name="deviceId"/>, or a
    /// <see cref="ActiveSessionResult"/> describing the failure (not found / not in progress).
    /// Mirrors the previous private helper semantics: throws are replaced by a NotInProgress
    /// failure so the caller (controller) can map the response without leaking exceptions.
    /// </summary>
    Task<ActiveSessionResult> GetActiveSessionAsync(MusicDbContext db, long sessionId, long deviceId, long ownerId, CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation of <see cref="ISyncSessionLookupService"/>.
/// </summary>
public class SyncSessionLookupService : ISyncSessionLookupService
{
    /// <inheritdoc />
    public async Task<DeviceSyncSession?> FindSessionAsync(MusicDbContext db, long sessionId, long deviceId, long ownerId, CancellationToken cancellationToken)
    {
        return await db.DeviceSyncSessions
            .Where(s => s.Id == sessionId && s.DeviceId == deviceId && s.Device.OwnerId == ownerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ActiveSessionResult> GetActiveSessionAsync(MusicDbContext db, long sessionId, long deviceId, long ownerId, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (session == null) return ActiveSessionResult.NotFound;
        if (session.Status != SyncSessionStatus.InProgress)
            return ActiveSessionResult.NotInProgress(sessionId, session.Status);
        return ActiveSessionResult.Succeeded(session);
    }
}