using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Services.Sync;
using MyMusic.Common.Targets;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongFileReplaceServiceSpecs
{
    private const string UploadPath = "/uploads/new.mp3";
    private const string M4aUploadPath = "/uploads/new.m4a";

    private readonly Scenario _scenario = new();
    private long _userId;
    private readonly Album _album;
    private readonly Device _device;

    public SongFileReplaceServiceSpecs()
    {
        _userId = _scenario.AdminUser.Id;
        _album = _scenario.CreateAlbum("Album", _scenario.CreateArtist("Artist"));
        _device = _scenario.CreateDevice("Phone");
    }

    private SongFileReplaceService CreateService(ISongFileUpdateService? songFileUpdate = null) =>
        new(_scenario.DbContext, _scenario.FileSystem, _scenario.AdvisoryLocks,
            _scenario.FileTransactions, songFileUpdate ?? _scenario.CreateSongFileUpdateService(),
            new SyncPathResolver(), Substitute.For<ILogger<SongFileReplaceService>>());

    /// <summary>
    /// Writes the file to upload: another song's tags, around audio that differs from every other file's.
    /// </summary>
    private void WriteUpload() =>
        MockMusicFile.CreateWithDifferentContent(_scenario.FileSystem, UploadPath, "Other Title", "Other Album",
            ["Other Artist"], ["Pop"], year: 1999);

    private TagLib.File OpenFile(string path) =>
        TagLib.File.Create(new FileSystemFileAbstraction(_scenario.FileSystem.FileInfo.New(path)));

    /// <summary>
    /// Returns the content of the file without any of its tags, so only its audio.
    /// </summary>
    private byte[] ReadAudio(string path)
    {
        var copy = $"/audio/{Guid.NewGuid()}{Path.GetExtension(path)}";
        _scenario.FileSystem.Directory.CreateDirectory("/audio");
        _scenario.FileSystem.File.Copy(path, copy);

        using (var file = OpenFile(copy))
        {
            file.RemoveTags(TagLib.TagTypes.AllTags);
            file.Save();
        }

        return _scenario.FileSystem.File.ReadAllBytes(copy);
    }

    private string CalculateChecksum(string path) =>
        ChecksumService.CalculateChecksum(_scenario.FileSystem, ChecksumService.CreateChecksumAlgorithm(), path);

    [Fact]
    public async Task ReplaceAsync_WritesSongMetadataIntoNewFile_DroppingTheTagsItCameWith()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        WriteUpload();

        // Act
        await CreateService().ReplaceAsync(_userId, song.Id, UploadPath);

        // Assert
        var reloaded = _scenario.LoadSong(song.Id);
        using var file = OpenFile(reloaded.RepositoryPath);
        file.Tag.ShouldSatisfyAllConditions(
            () => file.Tag.Title.ShouldBe("Song"),
            () => file.Tag.Album.ShouldBe("Album"),
            () => file.Tag.Performers.ShouldBe(["Artist"]),
            () => file.Tag.AlbumArtists.ShouldBe(["Artist"]),
            // The song has no genres nor year, unlike the uploaded file
            () => file.Tag.Genres.ShouldBeEmpty(),
            () => file.Tag.Year.ShouldBe(0u)
        );
        reloaded.Title.ShouldBe("Song");
        reloaded.AlbumId.ShouldBe(_album.Id);
    }

    [Fact]
    public async Task ReplaceAsync_KeepsTagsOfCurrentFileThatTheSongDoesNotStore()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        using (var current = OpenFile(song.RepositoryPath))
        {
            current.Tag.Comment = "Ripped from vinyl";
            current.Tag.Composers = ["The Composer"];
            current.Save();
        }

        WriteUpload();

        // Act
        await CreateService().ReplaceAsync(_userId, song.Id, UploadPath);

        // Assert
        using var file = OpenFile(_scenario.LoadSong(song.Id).RepositoryPath);
        file.Tag.Comment.ShouldBe("Ripped from vinyl");
        file.Tag.Composers.ShouldBe(["The Composer"]);
    }

    [Fact]
    public async Task ReplaceAsync_ReplacesAudio_UpdatesFileFieldsAndMarksDevicesForDownload()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var audioBefore = ReadAudio(song.RepositoryPath);
        WriteUpload();
        var uploadedAudio = ReadAudio(UploadPath);

        // Act
        var result = await CreateService().ReplaceAsync(_userId, song.Id, UploadPath);

        // Assert
        var reloaded = _scenario.LoadSong(song.Id);
        var audioAfter = ReadAudio(reloaded.RepositoryPath);
        using var file = OpenFile(reloaded.RepositoryPath);

        reloaded.ShouldSatisfyAllConditions(
            () => result.Id.ShouldBe(song.Id),
            () => reloaded.RepositoryPath.ShouldBe(song.RepositoryPath),
            () => audioAfter.ShouldBe(uploadedAudio),
            () => audioAfter.ShouldNotBe(audioBefore),
            () => reloaded.Checksum.ShouldNotBe(song.Checksum),
            () => reloaded.Checksum.ShouldBe(CalculateChecksum(reloaded.RepositoryPath)),
            () => reloaded.Size.ShouldBe(_scenario.FileSystem.FileInfo.New(reloaded.RepositoryPath).Length),
            () => reloaded.Duration.ShouldBe(file.Properties.Duration),
            () => reloaded.FileModifiedAt.ShouldNotBeNull(),
            () => reloaded.FileModifiedAt!.Value.ShouldBeGreaterThan(song.FileModifiedAt ?? DateTime.MinValue),
            () => reloaded.ModifiedAt.ShouldBeGreaterThan(song.ModifiedAt)
        );

        var songDevice = reloaded.Devices.ShouldHaveSingleItem();
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
        songDevice.SyncActionReason.ShouldBe("Song file replaced");
        songDevice.DevicePath.ShouldBe("/music/Song.mp3");
        songDevice.RequestedPath.ShouldBeNull();

        // The uploaded file is left where it was: its owner cleans it up
        _scenario.FileSystem.File.Exists(UploadPath).ShouldBeTrue();
    }

    [Fact]
    public async Task ReplaceAsync_FileOfAnotherFormat_ChangesExtensionInRepositoryAndOnDevices()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var tracked = _scenario.DbContext.Songs.Single(s => s.Id == song.Id);
        var neverDownloaded = _scenario.CreateSongDevice(_scenario.CreateDevice("Laptop"), tracked,
            "/music/Song.mp3", syncAction: SongSyncAction.Download);
        var removed = _scenario.CreateSongDevice(_scenario.CreateDevice("Tablet"), tracked, "/music/Song.mp3",
            lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Remove);
        MockMusicFile.CreateM4a(_scenario.FileSystem, M4aUploadPath);

        // Act
        await CreateService().ReplaceAsync(_userId, song.Id, M4aUploadPath);

        // Assert
        var reloaded = _scenario.LoadSong(song.Id);
        reloaded.RepositoryPath.ShouldBe(Path.ChangeExtension(song.RepositoryPath, ".m4a"));
        _scenario.FileSystem.File.Exists(song.RepositoryPath).ShouldBeFalse();
        reloaded.Checksum.ShouldBe(CalculateChecksum(reloaded.RepositoryPath));

        using (var file = OpenFile(reloaded.RepositoryPath))
        {
            file.Tag.Title.ShouldBe("Song");
            file.Tag.Album.ShouldBe("Album");
            file.Tag.Performers.ShouldBe(["Artist"]);
            reloaded.Duration.ShouldBe(file.Properties.Duration);
        }

        // A file the device holds stays where the device reports it, until the next sync moves it
        var synced = reloaded.Devices.Single(sd => sd.DeviceId == _device.Id);
        synced.SyncAction.ShouldBe(SongSyncAction.Download);
        synced.DevicePath.ShouldBe("/music/Song.mp3");
        synced.RequestedPath.ShouldBe("/music/Song.m4a");

        // A file the device never had is simply created at the new path
        var pending = reloaded.Devices.Single(sd => sd.Id == neverDownloaded.Id);
        pending.DevicePath.ShouldBe("/music/Song.m4a");
        pending.RequestedPath.ShouldBe("/music/Song.m4a");

        // A copy waiting to be removed is still removed from where it is
        var leaving = reloaded.Devices.Single(sd => sd.Id == removed.Id);
        leaving.SyncAction.ShouldBe(SongSyncAction.Remove);
        leaving.DevicePath.ShouldBe("/music/Song.mp3");
        leaving.RequestedPath.ShouldBeNull();
    }

    [Fact]
    public async Task ReplaceAsync_FileOfAnotherFormat_AvoidsDevicePathsAlreadyTaken()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var other = _scenario.CreateSong("Other");
        _scenario.CreateSongDevice(_device, other, "/music/Song.m4a", lastSyncedModifiedAt: DateTime.UtcNow);
        MockMusicFile.CreateM4a(_scenario.FileSystem, M4aUploadPath);

        // Act
        await CreateService().ReplaceAsync(_userId, song.Id, M4aUploadPath);

        // Assert
        _scenario.LoadSong(song.Id).Devices.ShouldHaveSingleItem().RequestedPath.ShouldBe("/music/Song (2).m4a");
    }

    [Fact]
    public async Task ReplaceAsync_UnsupportedFormat_ThrowsAndLeavesSongUntouched()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var filesBefore = _scenario.ReadMusicFiles();
        WriteUpload();
        _scenario.FileSystem.File.Copy(UploadPath, "/uploads/new.ogg");

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(() => CreateService().ReplaceAsync(_userId, song.Id, "/uploads/new.ogg"));
        _scenario.ShouldHaveUnchangedSongs([song], filesBefore);
    }

    [Fact]
    public async Task ReplaceAsync_UnreadableFile_ThrowsAndLeavesSongUntouched()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var filesBefore = _scenario.ReadMusicFiles();
        _scenario.FileSystem.Directory.CreateDirectory("/uploads");
        _scenario.FileSystem.File.WriteAllText(UploadPath, "This is not an audio file");

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(() => CreateService().ReplaceAsync(_userId, song.Id, UploadPath));
        _scenario.ShouldHaveUnchangedSongs([song], filesBefore);
    }

    [Fact]
    public async Task ReplaceAsync_FileUpdateFails_RestoresOriginalFile()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var filesBefore = _scenario.ReadMusicFiles();
        WriteUpload();
        var service = CreateService(
            new FailingSongFileUpdateService(_scenario.CreateSongFileUpdateService(), song.Id));

        // Act & Assert
        await Should.ThrowAsync<IOException>(() => service.ReplaceAsync(_userId, song.Id, UploadPath));
        _scenario.ShouldHaveUnchangedSongs([song], filesBefore);
    }

    [Fact]
    public async Task ReplaceAsync_SongOfAnotherUser_Throws()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var filesBefore = _scenario.ReadMusicFiles();
        WriteUpload();
        _userId = _scenario.CreateUser("Other", "other").Id;

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => CreateService().ReplaceAsync(_userId, song.Id, UploadPath));
        _scenario.ShouldHaveUnchangedSongs([song], filesBefore);
    }

    [Fact]
    public async Task ReplaceAsync_UnknownSong_Throws()
    {
        // Arrange
        WriteUpload();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => CreateService().ReplaceAsync(_userId, 123456, UploadPath));
    }
}
