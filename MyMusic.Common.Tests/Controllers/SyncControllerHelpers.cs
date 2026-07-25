using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
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

    public static ISyncPendingActionsService CreateSyncPendingActionsService(Scenario scenario) =>
        new SyncPendingActionsService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup,
            DevicesControllerHelpers.PathResolver,
            Microsoft.Extensions.Options.Options.Create(new Config
            {
                MusicRepositoryPath = "/music",
                DefaultNamingTemplate = "{{ simple_label }}{{ extension }}",
            }),
            Substitute.For<ILogger<SyncPendingActionsService>>());

    public static ISyncDeviceSongsService CreateSyncDeviceSongsService(Scenario scenario) =>
        new SyncDeviceSongsService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup,
            Substitute.For<ILogger<SyncDeviceSongsService>>());

    public static ISyncCheckService CreateSyncCheckService(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var config = Microsoft.Extensions.Options.Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = "{{ album.artist.name ?? artists[0].name ?? \"Unknown\" }}/{{ album.name ?? \"No Album\" }}/{{ simple_label }}{{ extension ?? \".mp3\" }}",
        });
        return new SyncCheckService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup,
            DevicesControllerHelpers.SessionLookup,
            factory ?? new SyncActionsServerFactory(),
            DevicesControllerHelpers.PathResolver,
            DevicesControllerHelpers.ComparisonHelper,
            config,
            Substitute.For<ILogger<SyncCheckService>>());
    }

    public static ISyncResolveConflictsService CreateSyncResolveConflictsService(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var config = Microsoft.Extensions.Options.Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = "{{ simple_label }}{{ extension }}",
        });
        return new SyncResolveConflictsService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup,
            DevicesControllerHelpers.SessionLookup,
            factory ?? new SyncActionsServerFactory(),
            DevicesControllerHelpers.PathResolver,
            config,
            Substitute.For<ILogger<SyncResolveConflictsService>>());
    }

    public static ISyncReportErrorService CreateSyncReportErrorService(Scenario scenario, ISyncActionsServerFactory? factory = null) =>
        new SyncReportErrorService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup,
            DevicesControllerHelpers.SessionLookup,
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ILogger<SyncReportErrorService>>());

    public static ISyncAcknowledgeService CreateSyncAcknowledgeService(Scenario scenario, ISyncCommitService? commitService = null) =>
        new SyncAcknowledgeService(
            scenario.DbContext,
            DevicesControllerHelpers.DeviceLookup,
            commitService ?? CreateRealAcknowledgeCommitService(),
            Substitute.For<ILogger<SyncAcknowledgeService>>());

    /// <summary>
    /// Builds a substitute <see cref="ISyncCommitService"/> whose
    /// <see cref="ISyncCommitService.AcknowledgeRecordsAsync"/> delegates to the real
    /// <see cref="SyncCommitService.AcknowledgeRecords"/> static implementation, mirroring the
    /// production wiring where <see cref="SyncAcknowledgeService"/> wraps that method.
    /// </summary>
    public static ISyncCommitService CreateRealAcknowledgeCommitService()
    {
        var service = Substitute.For<ISyncCommitService>();
        service.AcknowledgeRecordsAsync(Arg.Any<List<DeviceSyncSessionRecord>>(), Arg.Any<DateTime?>())
            .Returns(call =>
            {
                SyncCommitService.AcknowledgeRecords(
                    call.ArgAt<List<DeviceSyncSessionRecord>>(0),
                    call.ArgAt<DateTime?>(1));
                return Task.CompletedTask;
            });
        return service;
    }
}