using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Devices;

public class DeviceGetServiceSpecs
{
    private static DeviceGetService CreateService(Scenario scenario) =>
        new(scenario.DbContext, new DeviceLookupService());

    [Fact]
    public async Task Get_OwnedDevice_ReturnsDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");

        // Act
        var result = await CreateService(scenario).GetAsync(device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Device.Id.ShouldBe(device.Id);
        result.SongCount.ShouldBe(0);
    }

    [Fact]
    public async Task Get_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);

        // Act
        var result = await CreateService(scenario).GetAsync(otherDevice.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Get_UnknownDeviceId_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();

        // Act
        var result = await CreateService(scenario).GetAsync(9999, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Get_ReturnsSongCountForDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song1 = scenario.CreateSong("Song A");
        var song2 = scenario.CreateSong("Song B");
        scenario.CreateSongDevice(device, song1, "/music/a.mp3");
        scenario.CreateSongDevice(device, song2, "/music/b.mp3");

        // Act
        var result = await CreateService(scenario).GetAsync(device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.SongCount.ShouldBe(2);
    }

    [Fact]
    public async Task Get_CountsAllSongDevicesRegardlessOfAction()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song A");
        scenario.CreateSongDevice(device, song, "/music/a.mp3", syncAction: SongSyncAction.Download);
        scenario.CreateSongDevice(device, song, "/music/b.mp3", syncAction: SongSyncAction.Remove);

        // Act
        var result = await CreateService(scenario).GetAsync(device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.SongCount.ShouldBe(2);
    }

    [Fact]
    public async Task Get_DoesNotCountOtherDevicesSongDevices()
    {
        // Arrange
        var scenario = new Scenario();
        var d1 = scenario.CreateDevice("Phone1");
        var d2 = scenario.CreateDevice("Phone2");
        var song = scenario.CreateSong("Song A");
        scenario.CreateSongDevice(d1, song, "/music/a.mp3");
        scenario.CreateSongDevice(d2, song, "/music/a.mp3");

        // Act
        var result = await CreateService(scenario).GetAsync(d1.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.SongCount.ShouldBe(1);
    }

    [Fact]
    public async Task Get_DeviceWithoutSongDevices_ReturnsZeroCount()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Empty");

        // Act
        var result = await CreateService(scenario).GetAsync(device.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.SongCount.ShouldBe(0);
    }
}