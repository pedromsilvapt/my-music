using System.IO.Abstractions.TestingHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Devices;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Devices;

public class DeviceDeleteServiceSpecs
{
    private static (DeviceDeleteService service, ICurrentUser currentUser) CreateService(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var service = new DeviceDeleteService(
            scenario.DbContext,
            new DeviceLookupService(),
            currentUser,
            scenario.FileSystem,
            Substitute.For<ILogger<DeviceDeleteService>>());
        return (service, currentUser);
    }

    [Fact]
    public async Task Delete_OwnedDevice_DeletesDeviceAndAllAssociatedData()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote);
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(device.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        scenario.DbContext.Devices.Any(d => d.Id == device.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.DeviceId == device.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_UnknownDeviceId_ReturnsFalse()
    {
        // Arrange
        var scenario = new Scenario();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(9999, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_OtherUsersDevice_ReturnsFalse()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(otherDevice.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
        scenario.DbContext.Devices.Any(d => d.Id == otherDevice.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_OnlyDeletesTargetDeviceData()
    {
        // Arrange
        var scenario = new Scenario();
        var device1 = scenario.CreateDevice("Phone1");
        var device2 = scenario.CreateDevice("Phone2");
        var session1 = scenario.CreateSession(device1, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        var session2 = scenario.CreateSession(device2, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session1.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session2.Id, "/b.mp3", SyncRecordAction.CreateRemote);
        var (service, _) = CreateService(scenario);

        // Act
        await service.DeleteAsync(device1.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.Devices.Any(d => d.Id == device1.Id).ShouldBeFalse();
        scenario.DbContext.Devices.Any(d => d.Id == device2.Id).ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session1.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session2.Id).ShouldBeTrue();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session1.Id).ShouldBeFalse();
        scenario.DbContext.DeviceSyncSessionRecords.Any(r => r.SessionId == session2.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_DeletesMultipleSessionsWithRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var s1 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-10));
        var s2 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-5));
        var s3 = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled, startedAt: DateTime.UtcNow.AddDays(-1));
        scenario.AddRecord(s1.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(s2.Id, "/b.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(s2.Id, "/c.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(s3.Id, "/d.mp3", SyncRecordAction.CreateRemote);
        var (service, _) = CreateService(scenario);

        // Act
        await service.DeleteAsync(device.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessions.Count().ShouldBe(0);
        scenario.DbContext.DeviceSyncSessionRecords.Count().ShouldBe(0);
    }

    [Fact]
    public async Task Delete_DeletesSongDevicesForDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "/music/song.mp3");
        scenario.CreateSongDevice(device, song, "/music/song2.mp3");
        var (service, _) = CreateService(scenario);

        // Act
        await service.DeleteAsync(device.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.SongDevices.Any(sd => sd.DeviceId == device.Id).ShouldBeFalse();
        // Song itself is untouched; only the device-scoped SongDevices are removed.
        scenario.DbContext.Songs.Any(s => s.Id == song.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_DeletesSessionRecordsForAllDeviceSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.UpdateRemote);
        var (service, _) = CreateService(scenario);

        // Act
        await service.DeleteAsync(device.Id, CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessionRecords.Count(r => r.SessionId == session.Id).ShouldBe(0);
    }

    [Fact]
    public async Task Delete_CleansUpStagingDirectoriesForDeviceSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice("Phone");
        var repoPath = "/data";
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2), repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";
        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));
        var (service, _) = CreateService(scenario);

        // Act
        await service.DeleteAsync(device.Id, CancellationToken.None);

        // Assert
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_CleansUpStagingDirectoriesForMultipleSessions()
    {
        // Arrange
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice("Phone");
        var repoPath = "/data";
        var s1 = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2), repositoryPath: repoPath);
        var s2 = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled, startedAt: DateTime.UtcNow.AddDays(-1), repositoryPath: repoPath);
        var stagingDir1 = $"{repoPath}/.temp/sync-{s1.Id}";
        var stagingDir2 = $"{repoPath}/.temp/sync-{s2.Id}";
        mockFs.AddDirectory(stagingDir1);
        mockFs.AddDirectory(stagingDir2);
        var (service, _) = CreateService(scenario);

        // Act
        await service.DeleteAsync(device.Id, CancellationToken.None);

        // Assert
        mockFs.Directory.Exists(stagingDir1).ShouldBeFalse();
        mockFs.Directory.Exists(stagingDir2).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_SessionWithoutStagingDirectory_StillSucceeds()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        // No staging directory created on the (mock) filesystem.
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.DeleteAsync(device.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        scenario.DbContext.Devices.Any(d => d.Id == device.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_SessionWithNullRepositoryPath_DoesNotThrow()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        // CreateSession default repositoryPath is null.
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed, startedAt: DateTime.UtcNow.AddDays(-2));
        var (service, _) = CreateService(scenario);

        // Act & Assert
        await Should.NotThrowAsync(async () =>
        {
            await service.DeleteAsync(device.Id, CancellationToken.None);
        });
        scenario.DbContext.DeviceSyncSessions.Any(s => s.Id == session.Id).ShouldBeFalse();
    }
}