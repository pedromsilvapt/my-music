using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Input for a sync session start operation. Mirrors the server-side fields of
/// <c>SyncStartRequest</c> but lives in <see cref="MyMusic.Common"/> so the service has no
/// dependency on the Server DTO layer.
/// </summary>
public record SyncStartInput
{
    public bool DryRun { get; init; }

    public string? RepositoryPath { get; init; }

    public List<SyncStartScanError>? ScanErrors { get; init; }
}

/// <summary>
/// A single scan error reported by the client when starting a sync session.
/// </summary>
public record SyncStartScanError
{
    public required string FilePath { get; init; }

    public required string ErrorMessage { get; init; }
}

/// <summary>
/// Result of a sync session start operation. The controller maps this to
/// <c>SyncStartResponse</c>.
/// </summary>
public record SyncStartResult
{
    public required long SessionId { get; init; }
}

/// <summary>
/// Starts a new <see cref="DeviceSyncSession"/> for a device owned by the current user,
/// optionally recording client-reported scan errors. Extracted from
/// DevicesController.StartSync so the controller stays thin (input/output + DTO mapping only).
/// Reuses <see cref="IDeviceLookupService"/> for the device identity check and
/// <see cref="ISyncActionsServerFactory"/> to record scan errors.
/// </summary>
public interface ISyncStartService
{
    /// <summary>
    /// Starts a sync session for <paramref name="deviceId"/> owned by <paramref name="ownerId"/>.
    /// Returns <c>null</c> when no such device exists for the owner (mirrors the previous
    /// controller <c>NotFound</c> path).
    /// </summary>
    Task<SyncStartResult?> StartAsync(
        long deviceId,
        long ownerId,
        SyncStartInput input,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation of <see cref="ISyncStartService"/>.
/// </summary>
public class SyncStartService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncActionsServerFactory syncActionsServerFactory,
    ILogger<SyncStartService> logger) : ISyncStartService
{
    /// <inheritdoc />
    public async Task<SyncStartResult?> StartAsync(
        long deviceId,
        long ownerId,
        SyncStartInput input,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, ownerId, cancellationToken);
        if (device == null) return null;

        var session = new DeviceSyncSession
        {
            DeviceId = deviceId,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = input.DryRun,
            RepositoryPath = input.RepositoryPath,
        };

        db.DeviceSyncSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        if (input.ScanErrors is { Count: > 0 })
        {
            var syncActions = syncActionsServerFactory.Create(db, session.Id, deviceId, session.IsDryRun);
            foreach (var error in input.ScanErrors)
            {
                await syncActions.ActionError(error.FilePath, error.ErrorMessage, reason: $"Scan error: {error.ErrorMessage}", cancellationToken: cancellationToken);
            }
        }

        logger.LogInformation(
            "Started sync session {SessionId} for device {DeviceId} (DryRun: {IsDryRun}, RepositoryPath: {RepositoryPath})",
            session.Id, deviceId, session.IsDryRun, session.RepositoryPath);

        return new SyncStartResult { SessionId = session.Id };
    }
}