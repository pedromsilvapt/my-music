using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncDeviceSongsServiceSpecs
{
    private static SyncDeviceSongsService CreateService(Scenario scenario) =>
        new(
            scenario.DbContext,
            new DeviceLookupService(),
            Substitute.For<ILogger<SyncDeviceSongsService>>());

    [Fact]
    public async Task GetAsync_DeviceNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);

        // Act
        var result = await service.GetAsync(9999, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.GetAsync(otherDevice.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsAllSongDevicesForDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song1 = scenario.CreateSong("Song1");
        var song2 = scenario.CreateSong("Song2");
        scenario.CreateSongDevice(device, song1, "/music/song1.mp3");
        scenario.CreateSongDevice(device, song2, "/music/song2.mp3");

        // Act
        var result = await service.GetAsync(device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Songs.Count.ShouldBe(2);
        result.Songs.ShouldContain(s => s.Path == "/music/song1.mp3" && s.SongId == song1.Id);
        result.Songs.ShouldContain(s => s.Path == "/music/song2.mp3" && s.SongId == song2.Id);
    }

    [Fact]
    public async Task GetAsync_WithSyncAction_IncludesActionString()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "/music/song.mp3", syncAction: SongSyncAction.Download);

        // Act
        var result = await service.GetAsync(device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var songItem = result.Songs.Single();
        songItem.Action.ShouldBe(SongSyncAction.Download.ToString());
    }

    [Fact]
    public async Task GetAsync_WithNoSyncAction_ActionIsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        // Act
        var result = await service.GetAsync(device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var songItem = result.Songs.Single();
        songItem.Action.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_DoesNotReturnOtherDevicesSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var otherDevice = scenario.CreateDevice();
        var song1 = scenario.CreateSong("Song1");
        var song2 = scenario.CreateSong("Song2");
        scenario.CreateSongDevice(device, song1, "/music/song1.mp3");
        scenario.CreateSongDevice(otherDevice, song2, "/music/song2.mp3");

        // Act
        var result = await service.GetAsync(device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Songs.Count.ShouldBe(1);
        result.Songs.Single().Path.ShouldBe("/music/song1.mp3");
    }
}