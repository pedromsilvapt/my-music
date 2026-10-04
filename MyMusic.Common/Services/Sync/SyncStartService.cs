using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncStartService"/>.
/// </summary>
public class SyncStartService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ISyncActionsServerFactory syncActionsServerFactory,
    IFpcalcService fpcalc,
    IOptions<Config> config,
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

        if (input.Deduplicate && !fpcalc.IsAvailable())
        {
            throw new SyncStartValidationException(
                "Deduplication is not available: fpcalc (Chromaprint) is not installed on the server");
        }

        // A real run renames files on the device, so its template must be the one saved on the device:
        // the next sync would otherwise undo those paths
        if (input.DeviceOptions != null && !input.DryRun)
        {
            throw new SyncStartValidationException(
                "Device options can only be overridden in a dry run; save them to the device instead");
        }

        var namingTemplate = input.DeviceOptions != null
            ? input.DeviceOptions.NamingTemplate
            : device.NamingTemplate;

        var session = new DeviceSyncSession
        {
            DeviceId = deviceId,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = input.DryRun,
            Direction = input.Direction,
            RepositoryPath = input.RepositoryPath,
            Deduplicate = input.Deduplicate,
            NamingTemplate = namingTemplate ?? config.Value.DefaultNamingTemplate,
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
            "Started sync session {SessionId} for device {DeviceId} (DryRun: {IsDryRun}, Direction: {Direction}, Deduplicate: {Deduplicate}, RepositoryPath: {RepositoryPath}, NamingTemplate: {NamingTemplate})",
            session.Id, deviceId, session.IsDryRun, session.Direction, session.Deduplicate, session.RepositoryPath, session.NamingTemplate);

        return new SyncStartResult { SessionId = session.Id };
    }
}