using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongDevicePathPreviewServiceSpecs
{
    private const string DefaultNamingTemplate = "{{ year }}/{{ title }}{{ extension }}";

    private readonly Scenario _scenario = new();

    private Task<List<SongDevicePathPreview>> PreviewAsync(params Song[] songs)
    {
        _scenario.DbContext.ChangeTracker.Clear();
        var service = new SongDevicePathPreviewService(_scenario.DbContext, new SyncPathResolver(), Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = DefaultNamingTemplate,
        }));

        return service.PreviewAsync(_scenario.AdminUser.Id, songs.Select(s => s.Id).ToList(), CancellationToken.None);
    }

    [Fact]
    public async Task PreviewAsync_SongNotOnDevices_ReturnsEachDevicesTemplatePath()
    {
        var defaultDevice = _scenario.CreateDevice();
        var customDevice = _scenario.CreateDevice(namingTemplate: "All/{{ title }}{{ extension }}");
        var song = _scenario.CreateSong("Song", year: 2024);

        var previews = await PreviewAsync(song);

        previews.Count.ShouldBe(2);
        previews.Single(p => p.DeviceId == defaultDevice.Id).Path.ShouldBe("2024/Song.mp3");
        previews.Single(p => p.DeviceId == customDevice.Id).Path.ShouldBe("All/Song.mp3");
        previews.ShouldAllBe(p => p.SongId == song.Id);
    }

    [Fact]
    public async Task PreviewAsync_SongOnDevice_IsNotPreviewedForIt()
    {
        var device = _scenario.CreateDevice();
        var onDevice = _scenario.CreateSong("On device", year: 2024);
        var markedForRemoval = _scenario.CreateSong("Removed", year: 2024);
        var notOnDevice = _scenario.CreateSong("Song", year: 2024);
        _scenario.CreateSongDevice(device, onDevice, "2024/On device.mp3");
        _scenario.CreateSongDevice(device, markedForRemoval, "2024/Removed.mp3", syncAction: SongSyncAction.Remove);

        var previews = await PreviewAsync(onDevice, markedForRemoval, notOnDevice);

        previews.ShouldHaveSingleItem().SongId.ShouldBe(notOnDevice.Id);
    }

    [Fact]
    public async Task PreviewAsync_PathsTakenOnDeviceOrByOtherPreviewedSongs_AreMadeUnique()
    {
        var device = _scenario.CreateDevice();
        var existing = _scenario.CreateSong("Existing", year: 2024);
        var requested = _scenario.CreateSong("Requested", year: 2024);
        _scenario.CreateSongDevice(device, existing, "2024/Song.mp3");
        _scenario.CreateSongDevice(device, requested, "2024/Requested.mp3").RequestedPath = "2024/Song (2).mp3";
        _scenario.DbContext.SaveChanges();
        var first = _scenario.CreateSong("Song", year: 2024, repositoryPath: "/music/a/Song.mp3");
        var second = _scenario.CreateSong("Song", year: 2024, repositoryPath: "/music/b/Song.mp3");

        var previews = await PreviewAsync(first, second);

        previews.Select(p => p.Path).ShouldBe(["2024/Song (3).mp3", "2024/Song (4).mp3"]);
    }

    [Fact]
    public async Task PreviewAsync_OtherUsersSongsAndDevices_AreLeftOut()
    {
        var otherUser = _scenario.CreateUser("Other", "other");
        _scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var device = _scenario.CreateDevice();
        var otherSong = _scenario.CreateSong("Other song", ownerId: otherUser.Id);
        var song = _scenario.CreateSong("Song", year: 2024);

        var previews = await PreviewAsync(otherSong, song);

        var preview = previews.ShouldHaveSingleItem();
        preview.SongId.ShouldBe(song.Id);
        preview.DeviceId.ShouldBe(device.Id);
    }
}
