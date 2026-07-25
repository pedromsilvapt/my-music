using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;

namespace MyMusic.Common.Tests.Controllers;

/// <summary>
/// Provides real (non-mocked) instances of the dependencies required by
/// <see cref="MyMusic.Server.Controllers.SyncController"/> so that its specs
/// exercise the actual delegation wiring end-to-end. Reuses the shared lookup helpers
/// from <see cref="DevicesControllerHelpers"/> to keep identity operations centralized.
/// </summary>
internal static class SyncControllerHelpers
{
    public static ISyncStartService CreateSyncStartService(Scenario scenario, ISyncActionsServerFactory? factory = null) =>
        new SyncStartService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup,
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ILogger<SyncStartService>>());

    public static ISyncCompleteService CreateSyncCompleteService(Scenario scenario) =>
        new SyncCompleteService(
            scenario.DbContext,
            DevicesControllerHelpers.SessionLookup,
            Substitute.For<ILogger<SyncCompleteService>>());

    public static ISyncCancelService CreateSyncCancelService(Scenario scenario) =>
        new SyncCancelService(
            scenario.DbContext,
            DevicesControllerHelpers.SessionLookup,
            scenario.FileSystem,
            Substitute.For<ILogger<SyncCancelService>>());
}