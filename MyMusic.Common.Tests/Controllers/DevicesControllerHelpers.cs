using Microsoft.Extensions.Logging;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;

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

    public static IDeviceListService CreateDeviceListService(Scenario scenario) => new DeviceListService(scenario.DbContext);

    public static IDeviceGetService CreateDeviceGetService(Scenario scenario) =>
        new DeviceGetService(scenario.DbContext, DeviceLookup);

    public static IDeviceCreateService CreateDeviceCreateService(Scenario scenario, ICurrentUser currentUser) =>
        new DeviceCreateService(
            scenario.DbContext,
            currentUser,
            Substitute.For<ILogger<DeviceCreateService>>());

    public static IDeviceUpdateService CreateDeviceUpdateService(Scenario scenario, ICurrentUser currentUser) =>
        new DeviceUpdateService(
            scenario.DbContext,
            DeviceLookup,
            currentUser,
            Substitute.For<ILogger<DeviceUpdateService>>());
}