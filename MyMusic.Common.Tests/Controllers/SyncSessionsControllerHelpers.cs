using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;

namespace MyMusic.Common.Tests.Controllers;

/// <summary>
/// Provides real (non-mocked) instances of the dependencies required by
/// <see cref="MyMusic.Server.Controllers.SyncSessionsController"/> so that its specs
/// exercise the actual delegation wiring end-to-end. Reuses the shared lookup helpers
/// from <see cref="DevicesControllerHelpers"/> to keep identity operations centralized.
/// </summary>
internal static class SyncSessionsControllerHelpers
{
    public static ISyncSessionListService CreateSyncSessionListService(Scenario scenario) =>
        new SyncSessionListService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup);

    public static ISyncSessionRecordsQueryService CreateSyncSessionRecordsQueryService(Scenario scenario) =>
        new SyncSessionRecordsQueryService(
            scenario.DbContext,
            DevicesControllerHelpers.SessionLookup);

    public static ISyncSessionFilterValuesService CreateSyncSessionFilterValuesService(Scenario scenario) =>
        new SyncSessionFilterValuesService(scenario.DbContext);

    public static ISyncSessionDeleteService CreateSyncSessionDeleteService(Scenario scenario, IFileSystem? fileSystem = null) =>
        new SyncSessionDeleteService(
            scenario.DbContext,
            DevicesControllerHelpers.SessionLookup,
            fileSystem ?? scenario.FileSystem,
            Substitute.For<ILogger<SyncSessionDeleteService>>());

    public static ISyncSessionPruneService CreateSyncSessionPruneService(Scenario scenario, IFileSystem? fileSystem = null) =>
        new SyncSessionPruneService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup,
            fileSystem ?? scenario.FileSystem,
            Substitute.For<ILogger<SyncSessionPruneService>>());
}