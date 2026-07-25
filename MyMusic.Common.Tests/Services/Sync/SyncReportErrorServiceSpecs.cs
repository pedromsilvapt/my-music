using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncReportErrorServiceSpecs
{
    private static SyncReportErrorService CreateService(Scenario scenario, ISyncActionsServerFactory? factory = null) =>
        new(
            scenario.DbContext,
            new DeviceLookupService(),
            new SyncSessionLookupService(),
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ILogger<SyncReportErrorService>>());

    [Fact]
    public async Task ReportErrorAsync_DeviceNotFound_ReturnsDeviceNotFoundFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);

        // Act
        var result = await service.ReportErrorAsync(9999, scenario.AdminUser.Id, 1,
            new SyncReportErrorInput { FilePath = "/a.mp3", ErrorMessage = "boom" }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeFalse();
        result.Failure.ShouldBe(SyncReportErrorFailure.DeviceNotFound);
    }

    [Fact]
    public async Task ReportErrorAsync_OtherUsersDevice_ReturnsDeviceNotFoundFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        // Act
        var result = await service.ReportErrorAsync(otherDevice.Id, session.Id, scenario.AdminUser.Id,
            new SyncReportErrorInput { FilePath = "/a.mp3", ErrorMessage = "boom" }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeFalse();
        result.Failure.ShouldBe(SyncReportErrorFailure.DeviceNotFound);
    }

    [Fact]
    public async Task ReportErrorAsync_SessionNotFound_ReturnsSessionNotFoundFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.ReportErrorAsync(device.Id, 9999, scenario.AdminUser.Id,
            new SyncReportErrorInput { FilePath = "/a.mp3", ErrorMessage = "boom" }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeFalse();
        result.Failure.ShouldBe(SyncReportErrorFailure.SessionNotFound);
        result.SessionId.ShouldBe(9999);
    }

    [Fact]
    public async Task ReportErrorAsync_OtherUsersSession_ReturnsSessionNotFoundFailure()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var otherSession = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.ReportErrorAsync(device.Id, otherSession.Id, scenario.AdminUser.Id,
            new SyncReportErrorInput { FilePath = "/a.mp3", ErrorMessage = "boom" }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeFalse();
        result.Failure.ShouldBe(SyncReportErrorFailure.SessionNotFound);
    }

    [Fact]
    public async Task ReportErrorAsync_ValidDeviceAndSession_RecordsErrorAction()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var syncActions = Substitute.For<ISyncActionsServer>();
        var factory = Substitute.For<ISyncActionsServerFactory>();
        factory.Create(Arg.Any<MusicDbContext>(), session.Id, device.Id, Arg.Any<bool>()).Returns(syncActions);
        var service = CreateService(scenario, factory);

        var createdRecord = new DeviceSyncSessionRecord
        {
            SessionId = session.Id,
            FilePath = "/a.mp3",
            Action = SyncRecordAction.Error,
            SongId = song.Id,
        };
        syncActions.ActionError("/a.mp3", "boom", song.Id, reason: "boom", cancellationToken: CancellationToken.None)
            .Returns(createdRecord);

        // Act
        var result = await service.ReportErrorAsync(device.Id, session.Id, scenario.AdminUser.Id,
            new SyncReportErrorInput { FilePath = "/a.mp3", ErrorMessage = "boom", SongId = song.Id }, CancellationToken.None);

        // Assert
        result.Found.ShouldBeTrue();
        result.Record.ShouldBeSameAs(createdRecord);
        await syncActions.Received(1).ActionError("/a.mp3", "boom", song.Id, reason: "boom", cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task ReportErrorAsync_NullSongId_PassesNullToSyncActions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var syncActions = Substitute.For<ISyncActionsServer>();
        var factory = Substitute.For<ISyncActionsServerFactory>();
        factory.Create(Arg.Any<MusicDbContext>(), session.Id, device.Id, Arg.Any<bool>()).Returns(syncActions);
        var service = CreateService(scenario, factory);

        // Act
        await service.ReportErrorAsync(device.Id, session.Id, scenario.AdminUser.Id,
            new SyncReportErrorInput { FilePath = "/a.mp3", ErrorMessage = "boom" }, CancellationToken.None);

        // Assert
        await syncActions.Received(1).ActionError("/a.mp3", "boom", null, reason: "boom", cancellationToken: CancellationToken.None);
    }
}