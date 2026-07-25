using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Common.Tests.Controllers;

/// <summary>
/// Provides real (non-mocked) instances of the Phase 0 shared helper services so that
/// <see cref="DevicesController"/> specs exercise the actual delegation wiring end-to-end.
/// </summary>
internal static class DevicesControllerHelpers
{
    public static IDeviceLookupService DeviceLookup => new DeviceLookupService();

    public static ISyncSessionLookupService SessionLookup => new SyncSessionLookupService();

    public static ISyncPathResolver PathResolver => new SyncPathResolver();

    public static ISyncComparisonHelper ComparisonHelper => new SyncComparisonHelper();
}