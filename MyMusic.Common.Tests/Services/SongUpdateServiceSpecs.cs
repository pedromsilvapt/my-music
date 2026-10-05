using System.IO.Abstractions;
using System.IO.Hashing;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SongUpdateServiceSpecs
{
    private SongUpdateService CreateService(Scenario scenario, ISongFileUpdateService? songFileUpdate = null)
    {
        return new SongUpdateService(
            songFileUpdate ?? CreateSongFileUpdateService(scenario),
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            new AlbumUpsertService(),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<SongUpdateService>>());
    }

    private static SongFileUpdateService CreateSongFileUpdateService(Scenario scenario) =>
        new(scenario.FileSystem, Options.Create(new Config { MusicRepositoryPath = "/data" }));

    private (string checksum, string algorithm) SetupMusicFile(IFileSystem fileSystem, string repositoryPath, string ownerUsername)
    {
        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(repositoryPath)!);
        fileSystem.Directory.CreateDirectory(fileSystem.Path.Join("/data", ownerUsername));
        MockMusicFile.Create(fileSystem, repositoryPath, "My Song", "My Song Album", ["My Song Artist"], ["Rock"]);
        var algo = new XxHash128();
        var checksum = ChecksumService.CalculateChecksum(fileSystem, algo, repositoryPath);
        return (checksum, algo.GetType().Name);
    }

    [Fact]
    public async Task UpdateSong_SongOnDevice_SetsSyncActionToDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_SongOnMultipleDevices_SetsSyncActionToDownloadForAll()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device1 = scenario.CreateDevice("Phone");
        var device2 = scenario.CreateDevice("Tablet");
        AddSongToDevice(scenario.DbContext, song, device1, "/music/My Song.mp3");
        AddSongToDevice(scenario.DbContext, song, device2, "/music/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevices = scenario.DbContext.SongDevices
            .Where(sd => sd.SongId == song.Id)
            .ToList();
        songDevices.Count.ShouldBe(2);
        songDevices.ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_SongNotOnAnyDevice_DoesNotCreateSongDevices()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        scenario.DbContext.SongDevices.Count().ShouldBe(0);
    }

    [Fact]
    public async Task UpdateSong_SongDevicePendingRemove_DoesNotChangeSyncAction()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3",
            syncAction: SongSyncAction.Remove);

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    [Fact]
    public async Task UpdateSong_SongDeviceAlreadyPendingDownload_StaysDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3",
            syncAction: SongSyncAction.Download);

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_SyncedSongDevice_GetsMarkedForDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3",
            syncAction: null, lastSyncedModifiedAt: DateTime.UtcNow.AddDays(-1));

        var update = new SongUpdateModel { Lyrics = new ValueUpdate<string>("New lyrics") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task BatchUpdateSong_SongOnDevice_SetsSyncActionToDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        var result = await service.BatchUpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Success.ShouldBeTrue();
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_SameFileContent_DoesNotSetSyncAction()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3",
            syncAction: null, lastSyncedModifiedAt: DateTime.UtcNow.AddDays(-1));

        var firstUpdate = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };
        await service.UpdateSong(scenario.DbContext, song.Id, firstUpdate);

        var songAfterFirst = scenario.DbContext.Songs.First(s => s.Id == song.Id);
        var checksumAfterFirst = songAfterFirst.Checksum;

        var songDeviceAfterFirst = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDeviceAfterFirst.SyncAction.ShouldBe(SongSyncAction.Download);
        songDeviceAfterFirst.SyncAction = null;
        scenario.DbContext.SaveChanges();

        var secondUpdate = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, secondUpdate);

        // Assert
        var songAfterSecond = scenario.DbContext.Songs.First(s => s.Id == song.Id);
        songAfterSecond.Checksum.ShouldBe(checksumAfterFirst);

        var songDeviceAfterSecond = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDeviceAfterSecond.SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateSong_MixedDevices_OnlyNonRemoveDevicesGetDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device1 = scenario.CreateDevice("Phone");
        var device2 = scenario.CreateDevice("Tablet");
        AddSongToDevice(scenario.DbContext, song, device1, "/music/My Song.mp3",
            syncAction: null, lastSyncedModifiedAt: DateTime.UtcNow.AddDays(-1));
        AddSongToDevice(scenario.DbContext, song, device2, "/music/My Song.mp3",
            syncAction: SongSyncAction.Remove);

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevices = scenario.DbContext.SongDevices
            .Where(sd => sd.SongId == song.Id)
            .ToList();
        songDevices.Count.ShouldBe(2);
        songDevices.First(sd => sd.DeviceId == device1.Id).SyncAction.ShouldBe(SongSyncAction.Download);
        songDevices.First(sd => sd.DeviceId == device2.Id).SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    [Fact]
    public async Task UpdateSong_ChangeArtists_SetsSyncActionToDownload()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3");

        var newArtist = new Artist
        {
            Name = "New Artist",
            OwnerId = scenario.AdminUser.Id,
            Owner = scenario.AdminUser,
            CreatedAt = DateTime.UtcNow
        };
        scenario.DbContext.Add(newArtist);
        scenario.DbContext.SaveChanges();

        // Include both the new artist and the album artist (required by validation)
        var albumArtist = song.Album.Artist;
        var update = new SongUpdateModel
        {
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(newArtist.Id, null), new ArtistRef(albumArtist.Id, null)])
        };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == song.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);

        var updatedSong = scenario.DbContext.Songs
            .Include(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .First(s => s.Id == song.Id);
        updatedSong.Artists.Count.ShouldBe(2);
        updatedSong.Artists.Select(a => a.Artist.Name).ShouldContain("New Artist");
    }

    [Fact]
    public async Task UpdateSong_ChecksumChanged_UpdatesFileModifiedAt()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var originalFileModifiedAt = DateTime.UtcNow.AddDays(-10);
        var song = scenario.CreateSong("My Song",
            checksum: checksum, checksumAlgorithm: algo,
            repositoryPath: $"/data/My Song.mp3",
            fileModifiedAt: originalFileModifiedAt);

        // A title change rewrites the file -> new checksum
        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var updatedSong = scenario.DbContext.Songs.First(s => s.Id == song.Id);
        updatedSong.FileModifiedAt.ShouldNotBeNull();
        updatedSong.FileModifiedAt.Value.ShouldBeGreaterThan(originalFileModifiedAt);
    }

    [Fact]
    public async Task UpdateSong_ChecksumUnchanged_DoesNotUpdateFileModifiedAt()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var originalFileModifiedAt = DateTime.UtcNow.AddDays(-10);
        var song = scenario.CreateSong("My Song",
            checksum: checksum, checksumAlgorithm: algo,
            repositoryPath: $"/data/My Song.mp3",
            fileModifiedAt: originalFileModifiedAt);

        // First update rewrites the file (title change) -> new checksum
        var firstUpdate = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };
        await service.UpdateSong(scenario.DbContext, song.Id, firstUpdate);

        var songAfterFirst = scenario.DbContext.Songs.First(s => s.Id == song.Id);
        var fileModifiedAtAfterFirst = songAfterFirst.FileModifiedAt!.Value;
        var checksumAfterFirst = songAfterFirst.Checksum;

        // Second update with the same title -> identical file content -> checksum unchanged
        var secondUpdate = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, secondUpdate);

        // Assert
        var songAfterSecond = scenario.DbContext.Songs.First(s => s.Id == song.Id);
        songAfterSecond.Checksum.ShouldBe(checksumAfterFirst);
        songAfterSecond.FileModifiedAt.ShouldBe(fileModifiedAtAfterFirst);
    }

    [Fact]
    public async Task UpdateSong_TitleChange_MovesTheFileWithItsPath()
    {
        // Setup: a song whose title change generates another repository path
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, "/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo,
            repositoryPath: "/data/My Song.mp3");

        var result = await service.UpdateSong(scenario.DbContext, song.Id,
            new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") });

        // The file should now live at the song's new path, and nowhere else
        result.RepositoryPath.ShouldNotBe("/data/My Song.mp3");
        scenario.FileSystem.File.Exists(result.RepositoryPath).ShouldBeTrue();
        scenario.FileSystem.File.Exists("/data/My Song.mp3").ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSong_CommitFails_LeavesTheFileAtItsSavedPath()
    {
        // Setup: a song whose title change generates another repository path, and a commit that will fail
        var interceptor = new FailingCommitInterceptor();
        var scenario = new Scenario(interceptor);
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, "/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo,
            repositoryPath: "/data/My Song.mp3");
        var originalContent = scenario.FileSystem.File.ReadAllBytes("/data/My Song.mp3");
        interceptor.Armed = true;

        await Should.ThrowAsync<InvalidOperationException>(() => service.UpdateSong(scenario.DbContext, song.Id,
            new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") }));

        // The song keeps its old path and tags in the database, so its file should be there, unchanged
        scenario.FileSystem.Directory.GetFiles("/data", "*.mp3", SearchOption.AllDirectories)
            .ShouldBe(["/data/My Song.mp3"]);
        scenario.FileSystem.File.ReadAllBytes("/data/My Song.mp3").ShouldBe(originalContent);
    }

    [Fact]
    public async Task UpdateSong_CommitFails_KeepsTheOriginalFileContent()
    {
        // Setup: a song whose year is written into its file, and a commit that will fail
        var interceptor = new FailingCommitInterceptor();
        var scenario = new Scenario(interceptor);
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, "/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo,
            repositoryPath: "/data/My Song.mp3");
        var originalContent = scenario.FileSystem.File.ReadAllBytes("/data/My Song.mp3");
        interceptor.Armed = true;

        await Should.ThrowAsync<InvalidOperationException>(() => service.UpdateSong(scenario.DbContext, song.Id,
            new SongUpdateModel { Year = new StructValueUpdate<int>(1999) }));

        // The song keeps its old checksum in the database, so its file should keep its old tags
        scenario.FileSystem.Directory.GetFiles("/data", "*.mp3", SearchOption.AllDirectories)
            .ShouldBe(["/data/My Song.mp3"]);
        scenario.FileSystem.File.ReadAllBytes("/data/My Song.mp3").ShouldBe(originalContent);
    }

    // ---------------------------------------------------------------------
    // UpdateSong wraps all DB operations in a single transaction so that all
    // PostgreSQL triggers share the same txid_current() — enabling the
    // SongHistoryWorker to compact them into a single history row.
    // ---------------------------------------------------------------------
    [Fact]
    public async Task UpdateSong_WrapsInSingleTransaction()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act — after UpdateSong returns, no transaction should be left open
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert — the transaction was committed and is no longer active
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
    }

    // ---------------------------------------------------------------------
    // BatchUpdateSong wraps all DB operations in a single transaction
    // ---------------------------------------------------------------------
    [Fact]
    public async Task BatchUpdateSong_WrapsInSingleTransaction()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, $"/data/My Song.mp3", scenario.AdminUser.Username);
        var song = scenario.CreateSong("My Song", checksum: checksum, checksumAlgorithm: algo, repositoryPath: $"/data/My Song.mp3");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, song, device, "/music/My Song.mp3");

        var update = new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") };

        // Act
        await service.BatchUpdateSong(scenario.DbContext, song.Id, update);

        // Assert — the transaction was committed and is no longer active
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();
    }

    #region Album & Album Artist

    [Fact]
    public async Task UpdateSong_SameAlbumNameDifferentAlbumArtist_CreatesAlbumForThatArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var artistA = scenario.CreateArtist("Artist A");
        var albumA = scenario.CreateAlbum("Shared Name", artistA);
        var otherSong = CreateSongWithFile(scenario, "Other Song", albumA);
        var song = CreateSongWithFile(scenario, "My Song");
        var artistB = scenario.CreateArtist("Artist B");

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("Shared Name", new ArtistRef(artistB.Id))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(artistB.Id)]),
        };

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Album.Name.ShouldBe("Shared Name");
        result.Album.Id.ShouldNotBe(albumA.Id);
        result.Album.Artist!.Id.ShouldBe(artistB.Id);

        var albums = scenario.DbContext.Albums.AsNoTracking().Where(a => a.Name == "Shared Name").ToList();
        albums.Select(a => a.ArtistId).ShouldBe([artistA.Id, artistB.Id], ignoreOrder: true);
        scenario.DbContext.Songs.AsNoTracking().First(s => s.Id == otherSong.Id).AlbumId.ShouldBe(albumA.Id);
    }

    [Fact]
    public async Task UpdateSong_SameAlbumNameSameAlbumArtist_ReusesAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var artistA = scenario.CreateArtist("Artist A");
        var albumA = scenario.CreateAlbum("Shared Name", artistA);
        CreateSongWithFile(scenario, "Other Song", albumA);
        var song = CreateSongWithFile(scenario, "My Song");

        // The album artist is referenced by name, as when it is typed rather than picked
        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("Shared Name", new ArtistRef(Name: "Artist A"))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(artistA.Id)]),
        };

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Album.Id.ShouldBe(albumA.Id);
        scenario.DbContext.Albums.Count(a => a.Name == "Shared Name").ShouldBe(1);
        scenario.DbContext.Artists.Count(a => a.Name == "Artist A").ShouldBe(1);
    }

    [Fact]
    public async Task UpdateSong_OnlyAlbumArtistChanges_MovesSongToThatArtistsAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var artistA = scenario.CreateArtist("Artist A");
        var artistB = scenario.CreateArtist("Artist B");
        var albumA = scenario.CreateAlbum("Album", artistA);
        CreateSongWithFile(scenario, "Other Song", albumA);
        var song = CreateSongWithFile(scenario, "My Song", albumA, [artistA, artistB]);

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("Album", new ArtistRef(artistB.Id))),
        };

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Album.Id.ShouldNotBe(albumA.Id);
        result.Album.Name.ShouldBe("Album");
        result.Album.Artist!.Id.ShouldBe(artistB.Id);
        scenario.DbContext.Albums.Any(a => a.Id == albumA.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateSong_AlbumWithoutAlbumArtist_KeepsCurrentAlbumArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var albumArtistId = song.Album.ArtistId;

        var update = new SongUpdateModel { Album = new ValueUpdate<AlbumRef>(new AlbumRef("Renamed Album")) };

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Album.Name.ShouldBe("Renamed Album");
        result.Album.Artist!.Id.ShouldBe(albumArtistId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task UpdateSong_EmptyAlbumName_UsesPlaceholderAlbum(string? albumName)
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var albumArtistId = song.Album.ArtistId;

        var update = new SongUpdateModel { Album = new ValueUpdate<AlbumRef>(new AlbumRef(albumName)) };

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Album.Name.ShouldBe(Album.PlaceholderName);
        result.Album.Artist!.Id.ShouldBe(albumArtistId);
    }

    [Fact]
    public async Task UpdateSong_NewNameAsAlbumArtistAndSongArtist_CreatesOneArtist()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(Name: "New Artist"))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(Name: "New Artist")]),
        };

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        scenario.DbContext.Artists.Count(a => a.Name == "New Artist").ShouldBe(1);
        result.Artists.ShouldHaveSingleItem().Id.ShouldBe(result.Album.Artist!.Id);
    }

    [Fact]
    public async Task UpdateSong_ArtistNameSharedByTwoArtists_ResolvesToTheOneReferencedById()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        scenario.CreateArtist("Homonym");
        var secondHomonym = scenario.CreateArtist("Homonym");
        var song = CreateSongWithFile(scenario, "My Song");

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(secondHomonym.Id))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(Name: "Homonym")]),
        };

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Artists.ShouldHaveSingleItem().Id.ShouldBe(secondHomonym.Id);
        result.Album.Artist!.Id.ShouldBe(secondHomonym.Id);
    }

    [Fact]
    public async Task UpdateSong_AlbumArtistNotInNewArtists_Throws()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var otherArtist = scenario.CreateArtist("Other Artist");

        var update = new SongUpdateModel
        {
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(otherArtist.Id)]),
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(
            () => service.UpdateSong(scenario.DbContext, song.Id, update));
        exception.Message.ShouldContain("My Song Artist");
    }

    [Fact]
    public async Task UpdateSong_NewAlbumArtistNotInArtists_Throws()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var otherArtist = scenario.CreateArtist("Other Artist");

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(otherArtist.Id))),
        };

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(() => service.UpdateSong(scenario.DbContext, song.Id, update));
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Albums.Any(a => a.Name == "New Album").ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSong_LeavesAlbumAndArtistUnused_DeletesThem()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var oldAlbumId = song.AlbumId;
        var oldArtistId = song.Album.ArtistId;

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(Name: "New Artist"))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(Name: "New Artist")]),
        };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        scenario.DbContext.Albums.Any(a => a.Id == oldAlbumId).ShouldBeFalse();
        scenario.DbContext.Artists.Any(a => a.Id == oldArtistId).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSong_LeavesAlbumAndArtistUsedByOtherSongs_KeepsThem()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var artistA = scenario.CreateArtist("Artist A");
        var albumA = scenario.CreateAlbum("Album A", artistA);
        CreateSongWithFile(scenario, "Other Song", albumA);
        var song = CreateSongWithFile(scenario, "My Song", albumA);

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(Name: "New Artist"))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(Name: "New Artist")]),
        };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        scenario.DbContext.Albums.Any(a => a.Id == albumA.Id).ShouldBeTrue();
        scenario.DbContext.Artists.Any(a => a.Id == artistA.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateSong_ChangingAlbum_TakesTheAlbumAndArtistLocksOfAnImport()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var ownerId = scenario.AdminUser.Id;

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(Name: "New Artist"))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(Name: "New Artist")]),
        };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var acquired = scenario.AdvisoryLocks.Acquisitions.SelectMany(keys => keys).ToList();
        acquired.ShouldContain(AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "New Artist", "New Album"));
        acquired.ShouldContain(AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "New Artist"));
    }

    [Fact]
    public async Task BatchUpdateSong_SameAlbumNameDifferentAlbumArtist_CreatesAlbumAndDeletesUnusedOne()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var artistA = scenario.CreateArtist("Artist A");
        var albumA = scenario.CreateAlbum("Shared Name", artistA);
        CreateSongWithFile(scenario, "Other Song", albumA);
        var song = CreateSongWithFile(scenario, "My Song");
        var oldAlbumId = song.AlbumId;

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("Shared Name", new ArtistRef(Name: "Artist B"))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(Name: "Artist B")]),
        };

        // Act
        var result = await service.BatchUpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Success.ShouldBeTrue(result.Error);
        result.Song!.Album.Id.ShouldNotBe(albumA.Id);
        result.Song.Album.Artist!.Name.ShouldBe("Artist B");
        scenario.DbContext.Albums.Count(a => a.Name == "Shared Name").ShouldBe(2);
        scenario.DbContext.Albums.Any(a => a.Id == oldAlbumId).ShouldBeFalse();
    }

    [Fact]
    public async Task BatchUpdateSong_AlbumArtistNotInArtists_Fails()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(Name: "Other Artist"))),
        };

        // Act
        var result = await service.BatchUpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.ShouldContain("Other Artist");
    }

    #endregion Album & Album Artist

    #region Empty Update

    [Fact]
    public async Task UpdateSong_EmptyUpdateAfterArtistRename_RewritesTheFileAndMarksDevices()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (song, device) = await CreateSyncedSongOnDeviceAsync(scenario, service, "My Song");
        var checksumBefore = song.Checksum;
        var fileModifiedAtBefore = song.FileModifiedAt;

        RenameArtist(scenario, song.Album.ArtistId, "Renamed Artist");

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, new SongUpdateModel());

        // Assert
        var updatedSong = scenario.DbContext.Songs.AsNoTracking().First(s => s.Id == song.Id);
        updatedSong.Checksum.ShouldNotBe(checksumBefore);
        updatedSong.FileModifiedAt!.Value.ShouldBeGreaterThan(fileModifiedAtBefore!.Value);
        updatedSong.Label.ShouldBe("My Song - Renamed Artist");
        result.Label.ShouldBe("My Song - Renamed Artist");

        var songDevice = scenario.DbContext.SongDevices.AsNoTracking()
            .First(sd => sd.SongId == song.Id && sd.DeviceId == device.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSong_EmptyUpdateAfterArtistRename_MovesTheFileToTheRenamedArtistFolder()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (song, _) = await CreateSyncedSongOnDeviceAsync(scenario, service, "My Song");
        var pathBefore = song.RepositoryPath;

        RenameArtist(scenario, song.Album.ArtistId, "Renamed Artist");

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, new SongUpdateModel());

        // Assert
        result.RepositoryPath.ShouldBe("/data/admin/Renamed Artist/My Song Album/My Song - Renamed Artist.mp3");
        scenario.FileSystem.File.Exists(result.RepositoryPath).ShouldBeTrue();
        scenario.FileSystem.File.Exists(pathBefore).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSong_EmptyUpdateWithNothingRenamed_LeavesFileAndDevicesUntouched()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (song, device) = await CreateSyncedSongOnDeviceAsync(scenario, service, "My Song");
        var checksumBefore = song.Checksum;
        var pathBefore = song.RepositoryPath;

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, new SongUpdateModel());

        // Assert
        result.RepositoryPath.ShouldBe(pathBefore);
        scenario.DbContext.Songs.AsNoTracking().First(s => s.Id == song.Id).Checksum.ShouldBe(checksumBefore);
        scenario.DbContext.SongDevices.AsNoTracking()
            .First(sd => sd.SongId == song.Id && sd.DeviceId == device.Id).SyncAction.ShouldBeNull();
    }

    [Fact]
    public async Task BatchUpdateSong_EmptyUpdateAfterAlbumRename_RewritesAndMovesTheFile()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var (song, device) = await CreateSyncedSongOnDeviceAsync(scenario, service, "My Song");
        var checksumBefore = song.Checksum;
        var pathBefore = song.RepositoryPath;

        var album = scenario.DbContext.Albums.First(a => a.Id == song.AlbumId);
        album.Name = "Renamed Album";
        scenario.DbContext.SaveChanges();

        // Act
        var result = await service.BatchUpdateSong(scenario.DbContext, song.Id, new SongUpdateModel());

        // Assert
        result.Success.ShouldBeTrue(result.Error);
        result.Song!.RepositoryPath.ShouldBe("/data/admin/My Song Artist/Renamed Album/My Song - My Song Artist.mp3");
        scenario.FileSystem.File.Exists(result.Song.RepositoryPath).ShouldBeTrue();
        scenario.FileSystem.File.Exists(pathBefore).ShouldBeFalse();
        scenario.DbContext.Songs.AsNoTracking().First(s => s.Id == song.Id).Checksum.ShouldNotBe(checksumBefore);
        scenario.DbContext.SongDevices.AsNoTracking()
            .First(sd => sd.SongId == song.Id && sd.DeviceId == device.Id).SyncAction
            .ShouldBe(SongSyncAction.Download);
    }

    #endregion Empty Update

    #region Album By Id

    [Fact]
    public async Task UpdateSong_AlbumById_MovesSongToThatExactAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var artistB = scenario.CreateArtist("Artist B");
        var albumB = scenario.CreateAlbum("Target Album", artistB);
        var song = CreateSongWithFile(scenario, "My Song");
        var oldAlbumId = song.AlbumId;

        // The name and artist of the reference are ignored: the id alone picks the album
        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("Another Name") { Id = albumB.Id }),
            Artists = new ValueUpdate<List<ArtistRef>>(
                [new ArtistRef(song.Album.ArtistId), new ArtistRef(artistB.Id)]),
        };

        // Act
        var result = await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        result.Album.Id.ShouldBe(albumB.Id);
        result.Album.Name.ShouldBe("Target Album");
        result.Album.Artist!.Id.ShouldBe(artistB.Id);
        scenario.DbContext.Albums.Any(a => a.Name == "Another Name").ShouldBeFalse();
        scenario.DbContext.Albums.Any(a => a.Id == oldAlbumId).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSong_AlbumByIdWithoutItsAlbumArtist_Throws()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var artistB = scenario.CreateArtist("Artist B");
        var albumB = scenario.CreateAlbum("Target Album", artistB);
        var song = CreateSongWithFile(scenario, "My Song");

        var update = new SongUpdateModel { Album = new ValueUpdate<AlbumRef>(new AlbumRef { Id = albumB.Id }) };

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(
            () => service.UpdateSong(scenario.DbContext, song.Id, update));
        exception.Message.ShouldContain("Artist B");
    }

    [Fact]
    public async Task UpdateSong_AlbumByIdOfAnotherOwner_Throws()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var otherUser = scenario.CreateUser("Other", "other");
        var otherArtist = scenario.CreateArtist("Other Artist", otherUser.Id);
        var otherAlbum = scenario.CreateAlbum("Other Album", otherArtist, otherUser.Id);
        var song = CreateSongWithFile(scenario, "My Song");

        var update = new SongUpdateModel { Album = new ValueUpdate<AlbumRef>(new AlbumRef { Id = otherAlbum.Id }) };

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(
            () => service.UpdateSong(scenario.DbContext, song.Id, update));
        exception.Message.ShouldContain($"Album with ID {otherAlbum.Id} not found");
    }

    [Fact]
    public async Task UpdateSong_AlbumById_TakesTheLocksOfThatAlbum()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var artistB = scenario.CreateArtist("Artist B");
        var albumB = scenario.CreateAlbum("Target Album", artistB);
        var song = CreateSongWithFile(scenario, "My Song");
        var ownerId = scenario.AdminUser.Id;

        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef { Id = albumB.Id }),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(artistB.Id)]),
        };

        // Act
        await service.UpdateSong(scenario.DbContext, song.Id, update);

        // Assert
        var acquired = scenario.AdvisoryLocks.Acquisitions.SelectMany(keys => keys).ToList();
        acquired.ShouldContain(AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "Artist B", "Target Album"));
        acquired.ShouldContain(AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Artist B"));
    }

    [Fact]
    public void AlbumRef_FromJson_NeverCarriesAnId()
    {
        // Act
        var albumRef = JsonSerializer.Deserialize<AlbumRef>("""{"id": 5, "name": "Album"}""",
            JsonSerializerOptions.Web)!;

        // Assert
        albumRef.Name.ShouldBe("Album");
        albumRef.Id.ShouldBeNull();
    }

    #endregion Album By Id

    #region Ambient Transaction

    [Fact]
    public async Task UpdateSongsAsync_CallerCommits_UpdatesEverySongWithoutCommitting()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var first = CreateSongWithFile(scenario, "First");
        var second = CreateSongWithFile(scenario, "Second");
        var device = scenario.CreateDevice("Phone");
        AddSongToDevice(scenario.DbContext, first, device, "/music/First.mp3");
        AddSongToDevice(scenario.DbContext, second, device, "/music/Second.mp3");

        // Act
        await RunInTransactionAsync(scenario, async files =>
        {
            await service.UpdateSongsAsync(scenario.DbContext, files,
            [
                (first.Id, new SongUpdateModel { Title = new ValueUpdate<string>("First Updated") }),
                (second.Id, new SongUpdateModel { Title = new ValueUpdate<string>("Second Updated") }),
            ]);

            // The transaction stays the caller's to commit
            scenario.DbContext.Database.CurrentTransaction.ShouldNotBeNull();
        });

        // Assert
        var songs = scenario.DbContext.Songs.AsNoTracking().OrderBy(s => s.Id).ToList();
        songs.Select(s => s.Title).ShouldBe(["First Updated", "Second Updated"]);
        songs.ShouldAllBe(s => scenario.FileSystem.File.Exists(s.RepositoryPath));
        scenario.FileSystem.File.Exists("/data/First.mp3").ShouldBeFalse();
        scenario.FileSystem.File.Exists("/data/Second.mp3").ShouldBeFalse();
        scenario.DbContext.SongDevices.AsNoTracking().ToList()
            .ShouldAllBe(sd => sd.SyncAction == SongSyncAction.Download);
    }

    [Fact]
    public async Task UpdateSongsAsync_WithoutTransaction_Throws()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var files = Substitute.For<IFileTransaction>();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => service.UpdateSongsAsync(scenario.DbContext, files,
            [(song.Id, new SongUpdateModel { Title = new ValueUpdate<string>("Updated Title") })]));
        scenario.DbContext.Songs.AsNoTracking().First(s => s.Id == song.Id).Title.ShouldBe("My Song");
    }

    [Fact]
    public async Task UpdateSongsAsync_ChangingAlbum_TakesNoArtistOrAlbumLocks()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var first = CreateSongWithFile(scenario, "First");
        var second = CreateSongWithFile(scenario, "Second");
        var ownerId = scenario.AdminUser.Id;

        // Both songs move to the same album: with per-song locks, the second one would wait on the first forever
        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(Name: "New Artist"))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(Name: "New Artist")]),
        };

        // Act
        await RunInTransactionAsync(scenario, files =>
            service.UpdateSongsAsync(scenario.DbContext, files, [(first.Id, update), (second.Id, update)]));

        // Assert
        var acquired = scenario.AdvisoryLocks.Acquisitions.SelectMany(keys => keys).ToList();
        acquired.ShouldNotContain(AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "New Artist", "New Album"));
        acquired.ShouldNotContain(AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "New Artist"));
        scenario.DbContext.Songs.AsNoTracking().Select(s => s.AlbumId).Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task UpdateSongsAsync_KeepAlbumIds_KeepsTheAlbumLeftWithoutSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var oldAlbumId = song.AlbumId;
        var update = new SongUpdateModel { Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album")) };

        // Act
        await RunInTransactionAsync(scenario, files => service.UpdateSongsAsync(scenario.DbContext, files,
            [(song.Id, update)], new SongUpdateOptions { KeepAlbumIds = [oldAlbumId] }));

        // Assert
        scenario.DbContext.Albums.Any(a => a.Id == oldAlbumId).ShouldBeTrue();
        scenario.DbContext.Albums.First(a => a.Id == oldAlbumId).Songs.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateSongsAsync_KeepArtistIds_KeepsTheArtistLeftWithoutSongsAndAlbums()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var song = CreateSongWithFile(scenario, "My Song");
        var oldAlbumId = song.AlbumId;
        var oldArtistId = song.Album.ArtistId;
        var update = new SongUpdateModel
        {
            Album = new ValueUpdate<AlbumRef>(new AlbumRef("New Album", new ArtistRef(Name: "New Artist"))),
            Artists = new ValueUpdate<List<ArtistRef>>([new ArtistRef(Name: "New Artist")]),
        };

        // Act
        await RunInTransactionAsync(scenario, files => service.UpdateSongsAsync(scenario.DbContext, files,
            [(song.Id, update)], new SongUpdateOptions { KeepArtistIds = [oldArtistId] }));

        // Assert
        scenario.DbContext.Artists.Any(a => a.Id == oldArtistId).ShouldBeTrue();
        scenario.DbContext.Albums.Any(a => a.Id == oldAlbumId).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSongsAsync_SecondSongFails_RollbackRestoresEverySongRowAndFile()
    {
        // Arrange
        var scenario = new Scenario();
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var (first, second, device) = await CreateSyncedAlbumSongsAsync(scenario, album);
        var filesBefore = ReadMusicFiles(scenario);
        var service = CreateService(scenario,
            new FailingSongFileUpdateService(CreateSongFileUpdateService(scenario), second.Id));

        // Act: the album rename reaches the first song's file before the second song fails
        await Should.ThrowAsync<IOException>(() => RunInTransactionAsync(scenario, async files =>
        {
            album.Name = "Renamed Album";
            await scenario.DbContext.SaveChangesAsync();

            await service.UpdateSongsAsync(scenario.DbContext, files,
                [(first.Id, new SongUpdateModel()), (second.Id, new SongUpdateModel())]);
        }));

        // Assert
        AssertNothingChanged(scenario, album, [first, second], device, filesBefore);
    }

    [Fact]
    public async Task UpdateSongsAsync_CommitFails_RestoresEverySongRowAndFile()
    {
        // Arrange
        var interceptor = new FailingCommitInterceptor();
        var scenario = new Scenario(interceptor);
        var service = CreateService(scenario);
        var album = scenario.CreateAlbum("Album", scenario.CreateArtist("Artist"));
        var (first, second, device) = await CreateSyncedAlbumSongsAsync(scenario, album);
        var filesBefore = ReadMusicFiles(scenario);
        interceptor.Armed = true;

        // Act: both songs are rewritten and moved before the commit fails
        await Should.ThrowAsync<InvalidOperationException>(() => RunInTransactionAsync(scenario, async files =>
        {
            album.Name = "Renamed Album";
            await scenario.DbContext.SaveChangesAsync();

            await service.UpdateSongsAsync(scenario.DbContext, files,
                [(first.Id, new SongUpdateModel()), (second.Id, new SongUpdateModel())]);
        }));

        // Assert
        AssertNothingChanged(scenario, album, [first, second], device, filesBefore);
    }

    #endregion Ambient Transaction

    #region Helpers

    /// <summary>Plays the caller that owns the transaction: begins it, runs the updates, and commits.</summary>
    private static async Task RunInTransactionAsync(Scenario scenario, Func<IFileTransaction, Task> updates)
    {
        await using var transaction = await scenario.DbContext.Database.BeginTransactionAsync();
        await using var files = scenario.FileTransactions.Begin(scenario.DbContext);

        await updates(files);

        await transaction.CommitAsync();
    }

    /// <summary>Two songs of <paramref name="album"/>, as <see cref="CreateSyncedSongOnDeviceAsync"/> leaves them.</summary>
    private async Task<(Song first, Song second, Device device)> CreateSyncedAlbumSongsAsync(Scenario scenario,
        Album album)
    {
        var service = CreateService(scenario);
        var (first, device) = await CreateSyncedSongOnDeviceAsync(scenario, service, "First", album);
        var (second, _) = await CreateSyncedSongOnDeviceAsync(scenario, service, "Second", album, device: device);

        return (first, second, device);
    }

    private static Dictionary<string, byte[]> ReadMusicFiles(Scenario scenario) =>
        scenario.FileSystem.Directory.GetFiles("/data", "*.mp3", SearchOption.AllDirectories)
            .ToDictionary(path => path, path => scenario.FileSystem.File.ReadAllBytes(path));

    /// <summary>Asserts the album, its songs, their devices and their files are as they were before the operation.</summary>
    private static void AssertNothingChanged(Scenario scenario, Album album, List<Song> songsBefore, Device device,
        Dictionary<string, byte[]> filesBefore)
    {
        scenario.DbContext.ChangeTracker.Clear();

        scenario.DbContext.Albums.First(a => a.Id == album.Id).Name.ShouldBe("Album");

        foreach (var before in songsBefore)
        {
            var song = scenario.DbContext.Songs.First(s => s.Id == before.Id);
            song.RepositoryPath.ShouldBe(before.RepositoryPath);
            song.Checksum.ShouldBe(before.Checksum);
            song.FileModifiedAt.ShouldBe(before.FileModifiedAt);
        }

        scenario.DbContext.SongDevices.Where(sd => sd.DeviceId == device.Id).ToList()
            .ShouldAllBe(sd => sd.SyncAction == null);

        var files = ReadMusicFiles(scenario);
        files.Keys.ShouldBe(filesBefore.Keys, ignoreOrder: true);
        foreach (var (path, content) in filesBefore)
        {
            files[path].ShouldBe(content);
        }
    }

    /// <summary>Fails the file update of one song, as a full disk or a corrupt file would.</summary>
    private sealed class FailingSongFileUpdateService(ISongFileUpdateService inner, long failingSongId)
        : ISongFileUpdateService
    {
        public Task<SongFileUpdateResult> UpdateAsync(MusicDbContext db, IFileTransaction files, Song song,
            Func<string> downloadReason, CancellationToken cancellationToken = default) =>
            song.Id == failingSongId
                ? throw new IOException($"Cannot write the file of song {song.Id}")
                : inner.UpdateAsync(db, files, song, downloadReason, cancellationToken);
    }

    /// <summary>
    ///     A song whose file already carries the song's metadata and sits at the path it generates, on a device that
    ///     has nothing pending: whatever an update changes from here is caused by that update alone.
    /// </summary>
    private async Task<(Song song, Device device)> CreateSyncedSongOnDeviceAsync(Scenario scenario,
        SongUpdateService service, string title, Album? album = null, List<Artist>? artists = null,
        Device? device = null)
    {
        var song = CreateSongWithFile(scenario, title, album, artists);
        device ??= scenario.CreateDevice();
        await service.UpdateSong(scenario.DbContext, song.Id, new SongUpdateModel());

        AddSongToDevice(scenario.DbContext, song, device, $"/music/{title}.mp3",
            lastSyncedModifiedAt: DateTime.UtcNow);

        song = scenario.DbContext.Songs
            .AsNoTracking()
            .Include(s => s.Album)
            .ThenInclude(a => a.Artist)
            .First(s => s.Id == song.Id);

        return (song, device);
    }

    private static void RenameArtist(Scenario scenario, long artistId, string name)
    {
        var artist = scenario.DbContext.Artists.First(a => a.Id == artistId);
        artist.Name = name;
        scenario.DbContext.SaveChanges();
    }

    /// <summary>A song whose file exists in the repository, so its metadata can be written.</summary>
    private Song CreateSongWithFile(Scenario scenario, string title, Album? album = null, List<Artist>? artists = null)
    {
        var path = $"/data/{title}.mp3";
        var (checksum, algo) = SetupMusicFile(scenario.FileSystem, path, scenario.AdminUser.Username);

        return scenario.CreateSong(title, checksum: checksum, checksumAlgorithm: algo, repositoryPath: path,
            album: album, artists: artists);
    }

    private void AddSongToDevice(MusicDbContext db, Song song, Device device, string path,
        SongSyncAction? syncAction = null, DateTime? lastSyncedModifiedAt = null)
    {
        var sd = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = path,
            AddedAt = DateTime.UtcNow,
            SyncAction = syncAction,
            LastSyncedModifiedAt = lastSyncedModifiedAt,
        };
        db.Add(sd);
        db.SaveChanges();
    }

    #endregion
}
