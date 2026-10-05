using System.IO.Abstractions;
using System.IO.Hashing;
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
    private SongUpdateService CreateService(Scenario scenario)
    {
        var config = Options.Create(new Config { MusicRepositoryPath = "/data" });
        return new SongUpdateService(
            new SongFileUpdateService(scenario.FileSystem, config),
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            new AlbumUpsertService(),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<SongUpdateService>>());
    }

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

    #region Helpers

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
