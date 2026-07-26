using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Input for a sync acknowledge operation. Mirrors the server-side fields of
/// <c>AcknowledgeActionRequest</c> but lives in <see cref="MyMusic.Common"/> so the service has no
/// dependency on the Server DTO layer. <see cref="RecordIds"/> must be non-empty.
/// </summary>
public record SyncAcknowledgeInput
{
    public required List<long> RecordIds { get; init; }

    public DateTime? ModifiedAt { get; init; }
}

/// <summary>
/// Result of a sync acknowledge operation. The controller maps this to
/// <c>AcknowledgeActionResponse</c>. <see cref="Records"/> holds the acknowledged records on
/// success; it is empty when the device is not found or when the request is empty.
/// </summary>
public record SyncAcknowledgeResult
{
    public bool Found { get; init; }

    public bool BadRequest { get; init; }

    public List<DeviceSyncSessionRecord> Records { get; init; } = [];

    public static SyncAcknowledgeResult BadRequestResult => new() { Found = true, BadRequest = true };

    public static SyncAcknowledgeResult DeviceNotFound => new() { Found = false };

    public static SyncAcknowledgeResult Succeeded(List<DeviceSyncSessionRecord> records) => new()
    {
        Found = true,
        Records = records,
    };
}

/// <summary>
/// Acknowledges client-action sync records for a device owned by the current user, delegating the
/// per-record acknowledge logic to <see cref="ISyncCommitService.AcknowledgeRecordsAsync"/>
/// (per the refactor plan: "acknowledge wraps existing ISyncCommitService.AcknowledgeRecordsAsync").
/// Extracted from <c>DevicesController.AcknowledgeAction</c> as part of Phase 13 of the controllers
/// refactor so the controller stays thin (input/output + DTO mapping only). Reuses
/// <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> for the device identity check; does not reimplement the
/// acknowledge record mutation logic.
/// </summary>
public interface ISyncAcknowledgeService
{
    /// <summary>
    /// Acknowledges the records identified by <paramref name="input"/>.<see cref="SyncAcknowledgeInput.RecordIds"/>
    /// scoped to <paramref name="deviceId"/> owned by <paramref name="ownerId"/>. Returns a
    /// <see cref="SyncAcknowledgeResult"/> whose <see cref="SyncAcknowledgeResult.BadRequest"/>
    /// flag indicates an empty <c>RecordIds</c> request and whose <see cref="SyncAcknowledgeResult.Found"/>
    /// flag is <c>false</c> when the device does not exist for the owner (mirrors the previous
    /// controller <c>NotFound</c> path). On success <see cref="SyncAcknowledgeResult.Records"/>
    /// holds the acknowledged records.
    /// </summary>
    Task<SyncAcknowledgeResult> AcknowledgeAsync(
        long deviceId,
        long ownerId,
        SyncAcknowledgeInput input,
        CancellationToken cancellationToken);
}