using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongDevicesUpdateServiceSpecs
{
    private const string NamingTemplate = "{{ year }}/{{ title }}{{ extension }}";

    private readonly Scenario _scenario = new();

    private SongDevicesUpdateService CreateService() =>
        new(_scenario.DbContext, new SyncPathResolver(), Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = NamingTemplate,
        }));

    private Task UpdateAsync(IEnumerable<Song> songs, IEnumerable<(Device Device, bool Include)>? updates = null,
        IEnumerable<(Song Song, Device Device, string Path)>? paths = null,
        IEnumerable<(SongDevice Copy, string Path)>? copyPaths = null,
        IEnumerable<(SongDevice Copy, bool Include)>? copies = null) =>
        CreateService().UpdateAsync(_scenario.AdminUser.Id, new SongDevicesUpdateInput
        {
            SongIds = songs.Select(s => s.Id).ToList(),
            Updates = (updates ?? []).Select(u => new SongDeviceMembershipInput { DeviceId = u.Device.Id, Include = u.Include }).ToList(),
            Paths = (paths ?? []).Select(p => new SongDevicePathInput { SongId = p.Song.Id, DeviceId = p.Device.Id, Path = p.Path })
                .Concat((copyPaths ?? []).Select(p => new SongDevicePathInput
                {
                    SongId = p.Copy.SongId!.Value,
                    DeviceId = p.Copy.DeviceId,
                    SongDeviceId = p.Copy.Id,
                    Path = p.Path,
                }))
                .ToList(),
            Copies = (copies ?? []).Select(c => new SongDeviceCopyInput { SongDeviceId = c.Copy.Id, Include = c.Include }).ToList(),
        }, CancellationToken.None);

    private SongDevice? FindSongDevice(Song song, Device device)
    {
        _scenario.DbContext.ChangeTracker.Clear();
        return _scenario.DbContext.SongDevices.SingleOrDefault(sd => sd.SongId == song.Id && sd.DeviceId == device.Id);
    }

    private SongDevice FindCopy(SongDevice copy)
    {
        _scenario.DbContext.ChangeTracker.Clear();
        return _scenario.DbContext.SongDevices.Single(sd => sd.Id == copy.Id);
    }

    private SongDevice CreateSyncedSongDevice(Device device, Song song, string path) =>
        _scenario.CreateSongDevice(device, song, path, lastSyncedModifiedAt: DateTime.UtcNow);

    #region Membership

    [Fact]
    public async Task UpdateAsync_Include_AddsSongWithTemplatePathMarkedForDownload()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song", year: 2024);

        await UpdateAsync([song], [(device, true)]);

        var songDevice = FindSongDevice(song, device).ShouldNotBeNull();
        songDevice.DevicePath.ShouldBe("2024/Song.mp3");
        songDevice.RequestedPath.ShouldBeNull();
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
        songDevice.LastSyncedModifiedAt.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_IncludeSongsWithSamePath_MakesPathsUnique()
    {
        var device = _scenario.CreateDevice();
        var other = _scenario.CreateSong("Other", year: 2024);
        _scenario.CreateSongDevice(device, other, "2024/Song.mp3");
        var first = _scenario.CreateSong("Song", year: 2024, repositoryPath: "/music/a/Song.mp3");
        var second = _scenario.CreateSong("Song", year: 2024, repositoryPath: "/music/b/Song.mp3");

        await UpdateAsync([first, second], [(device, true)]);

        new[] { FindSongDevice(first, device)!.DevicePath, FindSongDevice(second, device)!.DevicePath }
            .ShouldBe(["2024/Song (2).mp3", "2024/Song (3).mp3"], ignoreOrder: true);
    }

    [Fact]
    public async Task UpdateAsync_IncludeSongAlreadyOnDevice_LeavesItUntouched()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song", year: 2024);
        CreateSyncedSongDevice(device, song, "Kept/Song.mp3");

        await UpdateAsync([song], [(device, true)]);

        var songDevice = FindSongDevice(song, device).ShouldNotBeNull();
        songDevice.DevicePath.ShouldBe("Kept/Song.mp3");
        songDevice.SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExcludeNeverDownloadedSong_DeletesSongDevice()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        _scenario.CreateSongDevice(device, song, "Song.mp3", syncAction: SongSyncAction.Download);

        await UpdateAsync([song], [(device, false)]);

        FindSongDevice(song, device).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExcludeSyncedSong_MarksSongDeviceForRemoval()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        CreateSyncedSongDevice(device, song, "Song.mp3");

        await UpdateAsync([song], [(device, false)]);

        var songDevice = FindSongDevice(song, device).ShouldNotBeNull();
        songDevice.SyncAction.ShouldBe(SongSyncAction.Remove);
        songDevice.SyncActionReason.ShouldBe("Song excluded from device");
    }

    [Fact]
    public async Task UpdateAsync_IncludeSongMarkedForRemoval_ClearsTheRemoval()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        _scenario.CreateSongDevice(device, song, "Song.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Remove);

        await UpdateAsync([song], [(device, true)]);

        var songDevice = FindSongDevice(song, device).ShouldNotBeNull();
        songDevice.SyncAction.ShouldBeNull();
        songDevice.DevicePath.ShouldBe("Song.mp3");
    }

    [Fact]
    public async Task UpdateAsync_OtherUsersDevice_IsIgnored()
    {
        var otherUser = _scenario.CreateUser("Other", "other");
        var otherDevice = _scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var song = _scenario.CreateSong("Song");

        await UpdateAsync([song], [(otherDevice, true)]);

        FindSongDevice(song, otherDevice).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_NoOwnedSongs_Throws()
    {
        var otherUser = _scenario.CreateUser("Other", "other");
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song", ownerId: otherUser.Id);

        var exception = await Should.ThrowAsync<Exception>(() => UpdateAsync([song], [(device, true)]));

        exception.Message.ShouldBe("No songs found");
    }

    #endregion

    #region Typed paths

    [Fact]
    public async Task UpdateAsync_IncludeWithTypedPath_AddsSongAtTypedPath()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song", year: 2024);

        await UpdateAsync([song], [(device, true)], [(song, device, "Custom/Typed name.mp3")]);

        var songDevice = FindSongDevice(song, device).ShouldNotBeNull();
        songDevice.DevicePath.ShouldBe("Custom/Typed name.mp3");
        songDevice.RequestedPath.ShouldBe("Custom/Typed name.mp3");
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateAsync_TypedPathTakesTheTemplatePathOfAnotherAddedSong_OtherSongGetsUniquePath()
    {
        var device = _scenario.CreateDevice();
        var typed = _scenario.CreateSong("Typed", year: 2024);
        var generated = _scenario.CreateSong("Song", year: 2024);

        await UpdateAsync([generated, typed], [(device, true)], [(typed, device, "2024/Song.mp3")]);

        FindSongDevice(typed, device)!.DevicePath.ShouldBe("2024/Song.mp3");
        FindSongDevice(generated, device)!.DevicePath.ShouldBe("2024/Song (2).mp3");
    }

    [Fact]
    public async Task UpdateAsync_TypedPathOfNeverDownloadedSong_ReplacesItsPath()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        _scenario.CreateSongDevice(device, song, "2024/Song.mp3", syncAction: SongSyncAction.Download);

        await UpdateAsync([song], paths: [(song, device, "Custom/Song.mp3")]);

        var songDevice = FindSongDevice(song, device).ShouldNotBeNull();
        songDevice.DevicePath.ShouldBe("Custom/Song.mp3");
        songDevice.RequestedPath.ShouldBe("Custom/Song.mp3");
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateAsync_TypedPathOfSyncedSong_KeepsDevicePathAndRequestsTheRename()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        CreateSyncedSongDevice(device, song, "2024/Song.mp3");

        await UpdateAsync([song], paths: [(song, device, " Custom/Song.mp3 ")]);

        // The device still reports the file at its current path, until the next sync renames it
        var songDevice = FindSongDevice(song, device).ShouldNotBeNull();
        songDevice.DevicePath.ShouldBe("2024/Song.mp3");
        songDevice.RequestedPath.ShouldBe("Custom/Song.mp3");
        songDevice.SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_TypedPathEqualToCurrentPath_CancelsThePendingRename()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var existing = CreateSyncedSongDevice(device, song, "2024/Song.mp3");
        existing.RequestedPath = "Custom/Song.mp3";
        _scenario.DbContext.SaveChanges();

        await UpdateAsync([song], paths: [(song, device, "2024/Song.mp3")]);

        FindSongDevice(song, device)!.RequestedPath.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_TypedPathReplacingPendingRename_FreesThePreviousRequest()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var other = _scenario.CreateSong("Other");
        var existing = CreateSyncedSongDevice(device, song, "2024/Song.mp3");
        existing.RequestedPath = "Custom/Song.mp3";
        CreateSyncedSongDevice(device, other, "2024/Other.mp3");
        _scenario.DbContext.SaveChanges();

        await UpdateAsync([song, other], paths: [(song, device, "Other/Song.mp3"), (other, device, "Custom/Song.mp3")]);

        FindSongDevice(song, device)!.RequestedPath.ShouldBe("Other/Song.mp3");
        FindSongDevice(other, device)!.RequestedPath.ShouldBe("Custom/Song.mp3");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/Custom/Song.mp3")]
    [InlineData("Custom//Song.mp3")]
    [InlineData("Custom/Song.mp3/")]
    [InlineData("../Song.mp3")]
    [InlineData("Custom/./Song.mp3")]
    [InlineData("Custom/Song.flac")]
    [InlineData("Custom/Song")]
    public async Task UpdateAsync_InvalidTypedPath_ThrowsAndSavesNothing(string path)
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");

        await Should.ThrowAsync<ValidationException>(() => UpdateAsync([song], [(device, true)], [(song, device, path)]));

        FindSongDevice(song, device).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_TypedPathLongerThanColumn_Throws()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var path = new string('a', 1021) + ".mp3";

        await Should.ThrowAsync<ValidationException>(() => UpdateAsync([song], [(device, true)], [(song, device, path)]));
    }

    [Fact]
    public async Task UpdateAsync_TypedPathUsedByAnotherSong_ThrowsAndSavesNothing()
    {
        var device = _scenario.CreateDevice();
        var other = _scenario.CreateSong("Other");
        CreateSyncedSongDevice(device, other, "Custom/Song.mp3");
        var added = _scenario.CreateSong("Added", year: 2024);
        var song = _scenario.CreateSong("Song");

        // One operation: the valid part (adding the other song) must not be saved either
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            UpdateAsync([added, song], [(device, true)], [(song, device, "Custom/Song.mp3")]));

        exception.Message.ShouldContain("already used");
        FindSongDevice(added, device).ShouldBeNull();
        FindSongDevice(song, device).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_TypedPathRequestedForAnotherSong_Throws()
    {
        var device = _scenario.CreateDevice();
        var other = _scenario.CreateSong("Other");
        var otherDevice = CreateSyncedSongDevice(device, other, "2024/Other.mp3");
        otherDevice.RequestedPath = "Custom/Song.mp3";
        _scenario.DbContext.SaveChanges();
        var song = _scenario.CreateSong("Song");

        await Should.ThrowAsync<ValidationException>(() =>
            UpdateAsync([song], [(device, true)], [(song, device, "Custom/Song.mp3")]));
    }

    [Fact]
    public async Task UpdateAsync_SameTypedPathForTwoSongs_Throws()
    {
        var device = _scenario.CreateDevice();
        var first = _scenario.CreateSong("First");
        var second = _scenario.CreateSong("Second");

        await Should.ThrowAsync<ValidationException>(() => UpdateAsync([first, second], [(device, true)],
            [(first, device, "Custom/Song.mp3"), (second, device, "Custom/Song.mp3")]));
    }

    [Fact]
    public async Task UpdateAsync_TypedPathOfSongNotOnDevice_Throws()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");

        var exception = await Should.ThrowAsync<ValidationException>(() =>
            UpdateAsync([song], paths: [(song, device, "Custom/Song.mp3")]));

        exception.Message.ShouldContain("not on it");
    }

    [Fact]
    public async Task UpdateAsync_TypedPathOfSongBeingRemoved_Throws()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        CreateSyncedSongDevice(device, song, "2024/Song.mp3");

        await Should.ThrowAsync<ValidationException>(() =>
            UpdateAsync([song], [(device, false)], [(song, device, "Custom/Song.mp3")]));

        FindSongDevice(song, device)!.SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_TypedPathOnOtherUsersDevice_Throws()
    {
        var otherUser = _scenario.CreateUser("Other", "other");
        var otherDevice = _scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var song = _scenario.CreateSong("Song");

        await Should.ThrowAsync<ValidationException>(() =>
            UpdateAsync([song], paths: [(song, otherDevice, "Custom/Song.mp3")]));
    }

    #endregion

    #region Copies

    [Fact]
    public async Task UpdateAsync_ExcludeSongWithTwoCopies_RemovesBothCopies()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var synced = CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        var neverDownloaded = _scenario.CreateSongDevice(device, song, "Copies/Song.mp3", syncAction: SongSyncAction.Download);

        await UpdateAsync([song], [(device, false)]);

        // The synced copy is left for the next sync to remove, the other one was never on the device
        _scenario.DbContext.ChangeTracker.Clear();
        var remaining = _scenario.DbContext.SongDevices.Where(sd => sd.SongId == song.Id).ToList();
        remaining.ShouldHaveSingleItem().Id.ShouldBe(synced.Id);
        remaining[0].SyncAction.ShouldBe(SongSyncAction.Remove);
        remaining.ShouldNotContain(sd => sd.Id == neverDownloaded.Id);
    }

    [Fact]
    public async Task UpdateAsync_IncludeSongWithAllCopiesMarkedForRemoval_RestoresThem()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var first = _scenario.CreateSongDevice(device, song, "Music/Song.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Remove);
        var second = _scenario.CreateSongDevice(device, song, "Copies/Song.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Remove);

        await UpdateAsync([song], [(device, true)]);

        FindCopy(first).SyncAction.ShouldBeNull();
        FindCopy(second).SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_IncludeSongWithACopyStaying_LeavesTheRemovedCopyRemoved()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song", year: 2024);
        var staying = CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        var removed = _scenario.CreateSongDevice(device, song, "Copies/Song.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Remove);

        await UpdateAsync([song], [(device, true)]);

        FindCopy(staying).SyncAction.ShouldBeNull();
        FindCopy(removed).SyncAction.ShouldBe(SongSyncAction.Remove);
        _scenario.DbContext.SongDevices.Count(sd => sd.SongId == song.Id).ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_TypedPathOfACopy_ChangesOnlyThatCopy()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var first = CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        var second = CreateSyncedSongDevice(device, song, "Copies/Song.mp3");

        await UpdateAsync([song], copyPaths: [(second, "Moved/Song.mp3")]);

        FindCopy(first).RequestedPath.ShouldBeNull();
        var moved = FindCopy(second);
        moved.DevicePath.ShouldBe("Copies/Song.mp3");
        moved.RequestedPath.ShouldBe("Moved/Song.mp3");
    }

    [Fact]
    public async Task UpdateAsync_TypedPathWithoutCopyOfSongWithTwoCopies_Throws()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var first = CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        CreateSyncedSongDevice(device, song, "Copies/Song.mp3");

        var exception = await Should.ThrowAsync<ValidationException>(() =>
            UpdateAsync([song], paths: [(song, device, "Moved/Song.mp3")]));

        exception.Message.ShouldContain("more than once");
        FindCopy(first).RequestedPath.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_TypedPathOfACopyOfAnotherSong_Throws()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var other = _scenario.CreateSong("Other");
        CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        var otherCopy = CreateSyncedSongDevice(device, other, "Music/Other.mp3");

        await Should.ThrowAsync<ValidationException>(() => UpdateAsync([song], paths:
            [], copyPaths: [(otherCopy, "Moved/Other.mp3")]));

        FindCopy(otherCopy).RequestedPath.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExcludeOneCopy_LeavesTheOtherCopy()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var kept = CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        var removed = CreateSyncedSongDevice(device, song, "Copies/Song.mp3");

        await UpdateAsync([song], copies: [(removed, false)]);

        FindCopy(kept).SyncAction.ShouldBeNull();
        var removedCopy = FindCopy(removed);
        removedCopy.SyncAction.ShouldBe(SongSyncAction.Remove);
        removedCopy.SyncActionReason.ShouldBe("Song excluded from device");
    }

    [Fact]
    public async Task UpdateAsync_ExcludeOneNeverDownloadedCopy_DeletesIt()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var kept = CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        var removed = _scenario.CreateSongDevice(device, song, "Copies/Song.mp3", syncAction: SongSyncAction.Download);

        await UpdateAsync([song], copies: [(removed, false)]);

        _scenario.DbContext.ChangeTracker.Clear();
        _scenario.DbContext.SongDevices.Where(sd => sd.SongId == song.Id).ShouldHaveSingleItem().Id.ShouldBe(kept.Id);
    }

    [Fact]
    public async Task UpdateAsync_IncludeOneCopyMarkedForRemoval_RestoresOnlyThatCopy()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var restored = _scenario.CreateSongDevice(device, song, "Music/Song.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Remove);
        var removed = _scenario.CreateSongDevice(device, song, "Copies/Song.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Remove);

        await UpdateAsync([song], copies: [(restored, true)]);

        var restoredCopy = FindCopy(restored);
        restoredCopy.SyncAction.ShouldBeNull();
        restoredCopy.SyncActionReason.ShouldBeNull();
        FindCopy(removed).SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    [Fact]
    public async Task UpdateAsync_TypedPathOfACopyBeingRemoved_ThrowsAndSavesNothing()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        var removed = CreateSyncedSongDevice(device, song, "Copies/Song.mp3");

        await Should.ThrowAsync<ValidationException>(() =>
            UpdateAsync([song], copyPaths: [(removed, "Moved/Song.mp3")], copies: [(removed, false)]));

        var copy = FindCopy(removed);
        copy.SyncAction.ShouldBeNull();
        copy.RequestedPath.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_IncludeOneCopyOfSongBeingRemovedFromDevice_ThrowsAndSavesNothing()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var staying = CreateSyncedSongDevice(device, song, "Music/Song.mp3");
        var removed = _scenario.CreateSongDevice(device, song, "Copies/Song.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Remove);

        await Should.ThrowAsync<ValidationException>(() =>
            UpdateAsync([song], [(device, false)], copies: [(removed, true)]));

        FindCopy(staying).SyncAction.ShouldBeNull();
        FindCopy(removed).SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    [Fact]
    public async Task UpdateAsync_CopyOfSongNotBeingUpdated_Throws()
    {
        var device = _scenario.CreateDevice();
        var song = _scenario.CreateSong("Song");
        var other = _scenario.CreateSong("Other");
        var otherCopy = CreateSyncedSongDevice(device, other, "Music/Other.mp3");

        await Should.ThrowAsync<ValidationException>(() => UpdateAsync([song], copies: [(otherCopy, false)]));

        FindCopy(otherCopy).SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_CopyOnOtherUsersDevice_Throws()
    {
        var otherUser = _scenario.CreateUser("Other", "other");
        var otherDevice = _scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var song = _scenario.CreateSong("Song");
        var otherCopy = CreateSyncedSongDevice(otherDevice, song, "Music/Song.mp3");

        await Should.ThrowAsync<ValidationException>(() => UpdateAsync([song], copies: [(otherCopy, false)]));

        FindCopy(otherCopy).SyncAction.ShouldBeNull();
    }

    #endregion
}
