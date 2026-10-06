using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongDevicesGetServiceSpecs
{
    private const string DefaultNamingTemplate = "{{ year }}/{{ title }}{{ extension }}";

    private readonly Scenario _scenario = new();

    private Task<SongDevicesResult> GetAsync(params Song[] songs)
    {
        _scenario.DbContext.ChangeTracker.Clear();
        var service = new SongDevicesGetService(_scenario.DbContext, new SyncPathResolver(), Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = DefaultNamingTemplate,
        }));

        return service.GetAsync(_scenario.AdminUser.Id, songs.Select(s => s.Id).ToList(), CancellationToken.None);
    }

    #region Songs

    [Fact]
    public async Task GetAsync_ReturnsTheSongsWithTheirArtists()
    {
        _scenario.CreateDevice();
        var second = _scenario.CreateSong("Second");
        var first = _scenario.CreateSong("First");
        _scenario.CreateSong("Not asked for");

        var result = await GetAsync(second, first);

        result.Songs.Select(s => s.Title).ShouldBe(["First", "Second"]);
        result.Songs.ShouldAllBe(s => s.Artists.Count > 0 && s.Artists.All(a => a.Artist != null));
    }

    #endregion

    #region Copies

    [Fact]
    public async Task GetAsync_SongTwiceOnDevice_ReturnsBothCopies()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var second = _scenario.CreateSongDevice(device, song, "Music/Song.mp3");
        var first = _scenario.CreateSongDevice(device, song, "Copies/Song.mp3");

        var result = await GetAsync(song);

        var entry = result.Devices.ShouldHaveSingleItem();
        entry.Copies.Select(sd => sd.Id).ShouldBe([first.Id, second.Id]);
        entry.Copies.Select(sd => sd.DevicePath).ShouldBe(["Copies/Song.mp3", "Music/Song.mp3"]);
        entry.PathPreviews.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_CopyDetails_AreReturned()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        _scenario.CreateSongDevice(device, song, "Music/Song.mp3", syncAction: SongSyncAction.Download).RequestedPath = "Typed/Song.mp3";
        _scenario.DbContext.SaveChanges();

        var result = await GetAsync(song);

        var copy = result.Devices.ShouldHaveSingleItem().Copies.ShouldHaveSingleItem();
        copy.SongId.ShouldBe(song.Id);
        copy.RequestedPath.ShouldBe("Typed/Song.mp3");
        copy.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task GetAsync_SongsOfTheDeviceThatWereNotAskedFor_AreLeftOut()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var other = _scenario.CreateSong("Other");
        _scenario.CreateSongDevice(device, song, "Music/Song.mp3");
        _scenario.CreateSongDevice(device, other, "Music/Other.mp3");

        var result = await GetAsync(song);

        result.Devices.ShouldHaveSingleItem().Copies.ShouldHaveSingleItem().SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task GetAsync_DeviceWithoutTheSongs_IsReturnedWithoutCopies()
    {
        var withSong = _scenario.CreateDevice("Phone");
        var withoutSong = _scenario.CreateDevice("Tablet");
        var song = _scenario.CreateSong("Song");
        _scenario.CreateSongDevice(withSong, song, "Music/Song.mp3");

        var result = await GetAsync(song);

        result.Devices.Select(d => d.Device.Id).ShouldBe([withSong.Id, withoutSong.Id]);
        result.Devices.Single(d => d.Device.Id == withoutSong.Id).Copies.ShouldBeEmpty();
    }

    #endregion

    #region Path previews

    [Fact]
    public async Task GetAsync_SongNotOnDevices_PreviewsEachDevicesTemplatePath()
    {
        var defaultDevice = _scenario.CreateDevice();
        var customDevice = _scenario.CreateDevice(namingTemplate: "All/{{ title }}{{ extension }}");
        var song = _scenario.CreateSong("Song", year: 2024);

        var result = await GetAsync(song);

        var defaultPreview = result.Devices.Single(d => d.Device.Id == defaultDevice.Id).PathPreviews.ShouldHaveSingleItem();
        defaultPreview.SongId.ShouldBe(song.Id);
        defaultPreview.Path.ShouldBe("2024/Song.mp3");
        result.Devices.Single(d => d.Device.Id == customDevice.Id).PathPreviews.ShouldHaveSingleItem().Path.ShouldBe("All/Song.mp3");
    }

    [Fact]
    public async Task GetAsync_SongOnDevice_IsNotPreviewedForIt()
    {
        var device = _scenario.CreateDevice();
        var onDevice = _scenario.CreateSong("On device", year: 2024);
        var markedForRemoval = _scenario.CreateSong("Removed", year: 2024);
        var notOnDevice = _scenario.CreateSong("Song", year: 2024);
        _scenario.CreateSongDevice(device, onDevice, "2024/On device.mp3");
        _scenario.CreateSongDevice(device, markedForRemoval, "2024/Removed.mp3", syncAction: SongSyncAction.Remove);

        var result = await GetAsync(onDevice, markedForRemoval, notOnDevice);

        // A song waiting to be removed is still a copy: adding it back keeps its path
        var entry = result.Devices.ShouldHaveSingleItem();
        entry.PathPreviews.ShouldHaveSingleItem().SongId.ShouldBe(notOnDevice.Id);
        entry.Copies.Select(sd => sd.SongId).ShouldBe([onDevice.Id, markedForRemoval.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetAsync_PathsTakenOnDeviceOrByOtherPreviewedSongs_AreMadeUnique()
    {
        var device = _scenario.CreateDevice();
        var existing = _scenario.CreateSong("Existing", year: 2024);
        var requested = _scenario.CreateSong("Requested", year: 2024);
        _scenario.CreateSongDevice(device, existing, "2024/Song.mp3");
        _scenario.CreateSongDevice(device, requested, "2024/Requested.mp3").RequestedPath = "2024/Song (2).mp3";
        _scenario.DbContext.SaveChanges();
        var first = _scenario.CreateSong("Song", year: 2024, repositoryPath: "/music/a/Song.mp3");
        var second = _scenario.CreateSong("Song", year: 2024, repositoryPath: "/music/b/Song.mp3");

        var result = await GetAsync(first, second);

        result.Devices.ShouldHaveSingleItem().PathPreviews.Select(p => p.Path)
            .ShouldBe(["2024/Song (3).mp3", "2024/Song (4).mp3"]);
    }

    #endregion

    [Fact]
    public async Task GetAsync_OtherUsersSongsAndDevices_AreLeftOut()
    {
        var otherUser = _scenario.CreateUser("Other", "other");
        _scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var device = _scenario.CreateDevice();
        var otherSong = _scenario.CreateSong("Other song", ownerId: otherUser.Id);
        var song = _scenario.CreateSong("Song", year: 2024);

        var result = await GetAsync(otherSong, song);

        result.Songs.ShouldHaveSingleItem().Id.ShouldBe(song.Id);
        var entry = result.Devices.ShouldHaveSingleItem();
        entry.Device.Id.ShouldBe(device.Id);
        entry.PathPreviews.ShouldHaveSingleItem().SongId.ShouldBe(song.Id);
    }
}
