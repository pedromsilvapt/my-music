using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class SyncControllerPendingActionsSpecs
{
    private const string NamingTemplate = "{{ simple_label }}{{ extension }}";

    private SyncController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new SyncController(
            Substitute.For<ILogger<SyncController>>(),
            currentUser,
            scenario.DbContext,
            scenario.FileSystem,
            SyncControllerHelpers.CreateSyncStartService(scenario),
            SyncControllerHelpers.CreateSyncCompleteService(scenario),
            SyncControllerHelpers.CreateSyncCancelService(scenario),
            Substitute.For<ISyncCommitService>(),
            SyncControllerHelpers.CreateSyncPendingActionsService(scenario),
            SyncControllerHelpers.CreateSyncDeviceSongsService(scenario),
            DevicesControllerHelpers.SessionLookup);
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
        var controller = CreateController(scenario);
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
    public async Task CreatePendingActions_RemoveSyncAction_CreatesDeleteLocalRecord()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Remove);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.DeleteLocal);
        response.Value.Records[0].FilePath.ShouldBe(expectedPath);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.DeleteLocal);
    }

    [Fact]
    public async Task CreatePendingActions_DownloadWithPreviousSync_CreatesUpdateLocalRecord()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
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
        var controller = CreateController(scenario);
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
        var controller = CreateController(scenario);
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
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Upload);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreatePendingActions_DeviceNotFound_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var session = scenario.CreateSession(scenario.CreateDevice(), status: SyncSessionStatus.InProgress);

        var response = await controller.CreatePendingActions(9999, session.Id, CancellationToken.None);

        response.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreatePendingActions_OtherUsersDevice_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);
        var controller = CreateController(scenario);

        var response = await controller.CreatePendingActions(otherDevice.Id, session.Id, CancellationToken.None);

        response.Result.ShouldBeOfType<NotFoundResult>();
        scenario.DbContext.DeviceSyncSessionRecords.Any().ShouldBeFalse();
    }
}

public class SyncControllerDeviceSongsSpecs
{
    private SyncController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new SyncController(
            Substitute.For<ILogger<SyncController>>(),
            currentUser,
            scenario.DbContext,
            scenario.FileSystem,
            SyncControllerHelpers.CreateSyncStartService(scenario),
            SyncControllerHelpers.CreateSyncCompleteService(scenario),
            SyncControllerHelpers.CreateSyncCancelService(scenario),
            Substitute.For<ISyncCommitService>(),
            SyncControllerHelpers.CreateSyncPendingActionsService(scenario),
            SyncControllerHelpers.CreateSyncDeviceSongsService(scenario),
            DevicesControllerHelpers.SessionLookup);
    }

    [Fact]
    public async Task GetDeviceSongs_ReturnsAllSongDevicesForDevice()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var song1 = scenario.CreateSong("Song1");
        var song2 = scenario.CreateSong("Song2");
        scenario.CreateSongDevice(device, song1, "/music/song1.mp3");
        scenario.CreateSongDevice(device, song2, "/music/song2.mp3");

        var response = await controller.GetDeviceSongs(device.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Songs.Count.ShouldBe(2);
        response.Value.Songs.ShouldContain(s => s.Path == "/music/song1.mp3" && s.SongId == song1.Id);
        response.Value.Songs.ShouldContain(s => s.Path == "/music/song2.mp3" && s.SongId == song2.Id);
    }

    [Fact]
    public async Task GetDeviceSongs_WithSyncAction_IncludesActionString()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "/music/song.mp3", syncAction: SongSyncAction.Download);

        var response = await controller.GetDeviceSongs(device.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        var songItem = response.Value.Songs.Single();
        songItem.Action.ShouldBe(SongSyncAction.Download.ToString());
    }

    [Fact]
    public async Task GetDeviceSongs_WithNoSyncAction_ActionIsNull()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var response = await controller.GetDeviceSongs(device.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        var songItem = response.Value.Songs.Single();
        songItem.Action.ShouldBeNull();
    }

    [Fact]
    public async Task GetDeviceSongs_DeviceNotFound_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var response = await controller.GetDeviceSongs(9999, CancellationToken.None);

        response.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDeviceSongs_OtherUsersDevice_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var controller = CreateController(scenario);

        var response = await controller.GetDeviceSongs(otherDevice.Id, CancellationToken.None);

        response.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDeviceSongs_DoesNotReturnOtherDevicesSongs()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();
        var otherDevice = scenario.CreateDevice();
        var song1 = scenario.CreateSong("Song1");
        var song2 = scenario.CreateSong("Song2");
        scenario.CreateSongDevice(device, song1, "/music/song1.mp3");
        scenario.CreateSongDevice(otherDevice, song2, "/music/song2.mp3");

        var response = await controller.GetDeviceSongs(device.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Songs.Count.ShouldBe(1);
        response.Value.Songs.Single().Path.ShouldBe("/music/song1.mp3");
    }
}