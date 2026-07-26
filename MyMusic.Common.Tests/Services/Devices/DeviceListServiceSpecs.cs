using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Devices;

public class DeviceListServiceSpecs
{
    private static DeviceListService CreateService(Scenario scenario) => new(scenario.DbContext);

    [Fact]
    public async Task List_NoFilters_ReturnsAllUserDevices()
    {
        // Arrange
        var scenario = new Scenario();
        var d1 = scenario.CreateDevice("Phone");
        var d2 = scenario.CreateDevice("Tablet");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: false, CancellationToken.None);

        // Assert
        result.Devices.Count.ShouldBe(2);
        result.Devices.Select(e => e.Device.Id).ShouldContain(d1.Id);
        result.Devices.Select(e => e.Device.Id).ShouldContain(d2.Id);
    }

    [Fact]
    public async Task List_OnlyReturnsCurrentUserDevices()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        scenario.CreateDevice("MyPhone");
        scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: false, CancellationToken.None);

        // Assert
        result.Devices.Count.ShouldBe(1);
        result.Devices[0].Device.Name.ShouldBe("MyPhone");
    }

    [Fact]
    public async Task List_Search_FiltersBySearchableText()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("Galaxy Phone");
        scenario.CreateDevice("iPad Tablet");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, "galaxy", null, includeSongs: false, CancellationToken.None);

        // Assert
        result.Devices.Count.ShouldBe(1);
        result.Devices[0].Device.Name.ShouldBe("Galaxy Phone");
    }

    [Fact]
    public async Task List_Filter_AppliesDslFilter()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("Phone");
        scenario.CreateDevice("Tablet");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, "name = \"Phone\"", includeSongs: false, CancellationToken.None);

        // Assert
        result.Devices.Count.ShouldBe(1);
        result.Devices[0].Device.Name.ShouldBe("Phone");
    }

    [Fact]
    public async Task List_ExcludeSongs_SongRefsAreNull()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song A");
        scenario.CreateSongDevice(device, song, "/music/a.mp3");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: false, CancellationToken.None);

        // Assert
        var entry = result.Devices.Single(e => e.Device.Id == device.Id);
        entry.SongRefs.ShouldBeNull();
    }

    [Fact]
    public async Task List_IncludeSongs_ReturnsSongRefs()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song A");
        scenario.CreateSongDevice(device, song, "/music/a.mp3");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: true, CancellationToken.None);

        // Assert
        var entry = result.Devices.Single(e => e.Device.Id == device.Id);
        entry.SongRefs.ShouldNotBeNull();
        entry.SongRefs.Count.ShouldBe(1);
        entry.SongRefs[0].SongId.ShouldBe(song.Id);
        entry.SongRefs[0].DevicePath.ShouldBe("/music/a.mp3");
    }

    [Fact]
    public async Task List_SongCount_CountsAllSongDevicesForDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song1 = scenario.CreateSong("Song A");
        var song2 = scenario.CreateSong("Song B");
        scenario.CreateSongDevice(device, song1, "/music/a.mp3");
        scenario.CreateSongDevice(device, song2, "/music/b.mp3");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: false, CancellationToken.None);

        // Assert
        var entry = result.Devices.Single(e => e.Device.Id == device.Id);
        entry.SongCount.ShouldBe(2);
    }

    [Fact]
    public async Task List_SongCount_ExcludesSongDevicesWithNullSongId()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song A");
        // One with a song id, one without (null song id - e.g. marked for removal)
        scenario.CreateSongDevice(device, song, "/music/a.mp3");
        scenario.CreateSongDevice(device, null, "/music/removed.mp3");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: false, CancellationToken.None);

        // Assert
        var entry = result.Devices.Single(e => e.Device.Id == device.Id);
        entry.SongCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_DeviceWithoutSongDevices_HasZeroCount()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("Empty");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: false, CancellationToken.None);

        // Assert
        result.Devices[0].SongCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_SongCount_DoesNotIncludeOtherDevicesSongDevices()
    {
        // Arrange
        var scenario = new Scenario();
        var d1 = scenario.CreateDevice("Phone1");
        var d2 = scenario.CreateDevice("Phone2");
        var song = scenario.CreateSong("Song A");
        scenario.CreateSongDevice(d1, song, "/music/a.mp3");
        scenario.CreateSongDevice(d2, song, "/music/a.mp3");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: false, CancellationToken.None);

        // Assert
        result.Devices.Single(e => e.Device.Id == d1.Id).SongCount.ShouldBe(1);
        result.Devices.Single(e => e.Device.Id == d2.Id).SongCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_IncludeSongs_EmptyDevice_ReturnsEmptySongRefs()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("Empty");

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: true, CancellationToken.None);

        // Assert: when includeSongs=true, SongRefs MUST be a non-null empty list,
        // never null, so clients can treat it as always-present.
        var entry = result.Devices.Single();
        entry.SongRefs.ShouldNotBeNull();
        entry.SongRefs.Count.ShouldBe(0);
    }

    [Fact]
    public async Task List_IncludeSongs_ReflectsSyncAction()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Song A");
        scenario.CreateSongDevice(device, song, "/music/a.mp3", syncAction: SongSyncAction.Download);

        // Act
        var result = await CreateService(scenario).ListAsync(scenario.AdminUser.Id, null, null, includeSongs: true, CancellationToken.None);

        // Assert
        var entry = result.Devices.Single(e => e.Device.Id == device.Id);
        entry.SongRefs.ShouldNotBeNull();
        entry.SongRefs[0].SyncAction.ShouldBe(SongSyncAction.Download);
    }
}