using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DevicesControllerCreatePendingActionsSpecs
{
    private const string NamingTemplate = "{{ simple_label }}{{ extension }}";

    private DevicesController CreateController(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var config = Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>();
        config.Value.Returns(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = NamingTemplate
        });

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            config,
            Substitute.For<System.IO.Abstractions.IFileSystem>(),
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ISyncCommitService>(),
            Substitute.For<ISyncUploadService>()
        );
    }

    private static string ComputeExpectedPath(Song song)
    {
        var namingStrategy = new TemplateNamingStrategy(NamingTemplate);
        var metadata = EntityConverter.ToSong(song);
        var naming = NamingMetadata.FromPath(song.RepositoryPath);
        return namingStrategy.Generate(metadata, naming);
    }


    [Fact]
    public async Task CreatePendingActions_DownloadSyncAction_CreatesCreateLocalRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        response.Value.Records[0].FilePath.ShouldBe(expectedPath);
        response.Value.Records[0].SongId.ShouldBe(song.Id);
        response.Value.Records[0].Acknowledged.ShouldBeFalse();

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        records[0].Acknowledged.ShouldBeFalse();
    }

    [Fact]
    public async Task CreatePendingActions_RemoveSyncAction_CreatesUnlinkRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Remove);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.Unlink);
        response.Value.Records[0].FilePath.ShouldBe(expectedPath);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.Unlink);
    }

    [Fact]
    public async Task CreatePendingActions_DownloadWithPreviousSync_CreatesUpdateLocalRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        var songDevice = scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);
        songDevice.LastSyncedModifiedAt = DateTime.UtcNow;
        scenario.DbContext.SaveChanges();

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.UpdateLocal);
    }

    [Fact]
    public async Task CreatePendingActions_PathChanged_CreatesOnlyCreateLocalRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        scenario.CreateSongDevice(device, song, "OldPath.mp3", syncAction: SongSyncAction.Download);
        var expectedNewPath = ComputeExpectedPath(song);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        response.Value.Records[0].FilePath.ShouldBe(expectedNewPath);
    }

    [Fact]
    public async Task CreatePendingActions_CalledTwice_DoesNotCreateDuplicateRecords()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);

        await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);
        await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreatePendingActions_UploadSyncAction_CreatesNoRecords()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Upload);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.Records.ShouldBeEmpty();
    }
}