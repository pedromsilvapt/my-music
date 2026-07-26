using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Input for a sync report-error operation. Mirrors the server-side fields of
/// <c>ReportSyncErrorRequest</c> but lives in <see cref="MyMusic.Common"/> so the service has no
/// dependency on the Server DTO layer.
/// </summary>
public record SyncReportErrorInput
{
    public required string FilePath { get; init; }

    public required string ErrorMessage { get; init; }

    public long? SongId { get; init; }
}

/// <summary>
/// Result of a sync report-error operation. The controller maps this to
/// <c>ReportSyncErrorResponse</c>. <see cref="Record"/> is the created <c>Error</c> record; it is
/// populated when <see cref="Found"/> is <c>true</c>.
/// </summary>
public record SyncReportErrorResult
{
    public bool Found { get; init; }

    public DeviceSyncSessionRecord? Record { get; init; }

    public SyncReportErrorFailure? Failure { get; init; }

    public long? SessionId { get; init; }

    public static SyncReportErrorResult DeviceNotFound => new() { Found = false, Failure = SyncReportErrorFailure.DeviceNotFound };

    public static SyncReportErrorResult SessionNotFound(long sessionId) => new()
    {
        Found = false,
        Failure = SyncReportErrorFailure.SessionNotFound,
        SessionId = sessionId,
    };

    public static SyncReportErrorResult Succeeded(DeviceSyncSessionRecord record) => new()
    {
        Found = true,
        Record = record,
    };
}

public enum SyncReportErrorFailure
{
    DeviceNotFound,
    SessionNotFound,
}

/// <summary>
/// Records a client-reported sync error for a device sync session owned by the current user.
/// Reuses <see cref="MyMusic.Common.Services.Devices.IDeviceLookupService"/> for the device identity check,
/// <see cref="ISyncSessionLookupService"/> for the session lookup, and
/// <see cref="ISyncActionsServerFactory"/> to persist the <c>Error</c> record.
/// </summary>
public interface ISyncReportErrorService
{
    /// <summary>
    /// Records an error for <paramref name="sessionId"/> scoped to <paramref name="deviceId"/>
    /// owned by <paramref name="ownerId"/>. Returns a <see cref="SyncReportErrorResult"/> whose
    /// <see cref="SyncReportErrorResult.Failure"/> indicates whether the device or session was not
    /// found (mirrors the previous controller <c>NotFound</c> paths); on success
    /// <see cref="SyncReportErrorResult.Record"/> holds the created <c>Error</c> record.
    /// </summary>
    Task<SyncReportErrorResult> ReportErrorAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        SyncReportErrorInput input,
        CancellationToken cancellationToken);
}