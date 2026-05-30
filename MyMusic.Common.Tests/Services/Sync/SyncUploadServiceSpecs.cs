using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncUploadServiceSpecs
{
    private readonly IMusicService _musicService = Substitute.For<IMusicService>();
    private readonly ISyncActionsServerFactory _syncActionsServerFactory = new SyncActionsServerFactory();
    private readonly ILogger<SyncUploadService> _logger = Substitute.For<ILogger<SyncUploadService>>();

    public SyncUploadServiceSpecs()
    {
        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Song>());
    }

    private SyncUploadService CreateService(MusicDbContext db, IFileSystem? fileSystem = null)
    {
        return new SyncUploadService(
            db,
            fileSystem ?? new MockFileSystem(),
            _musicService,
            _syncActionsServerFactory,
            _logger);
    }

    private Device CreateDevice(MusicDbContext db, long ownerId)
    {
        var device = new Device
        {
            Name = $"Device-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            Songs = []
        };
        db.Add(device);
        db.SaveChanges();
        return device;
    }

    private DeviceSyncSession CreateSession(MusicDbContext db, Device device, bool isDryRun = false, string? repositoryPath = null)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = isDryRun,
            RepositoryPath = repositoryPath,
            Records = []
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private Song CreateSong(MusicDbContext db, long ownerId, string? checksum = null, string? checksumAlgorithm = null)
    {
        var artist = new Artist
        {
            Name = $"Artist-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        db.Add(artist);
        db.SaveChanges();

        var album = new Album
        {
            Name = $"Album-{Guid.NewGuid():N}",
            ArtistId = artist.Id,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.Add(album);
        db.SaveChanges();

        var song = new Song
        {
            Title = $"Song-{Guid.NewGuid():N}",
            Label = "Label",
            AlbumId = album.Id,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            RepositoryPath = "/music/song.mp3",
            Checksum = checksum ?? "abc123",
            ChecksumAlgorithm = checksumAlgorithm ?? "XxHash128",
            Size = 1000,
            Duration = TimeSpan.FromSeconds(180),
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        db.Songs.Add(song);
        db.SaveChanges();
        return song;
    }

    private SongDevice CreateSongDevice(MusicDbContext db, Device device, Song song, string devicePath)
    {
        var sd = new SongDevice
        {
            DeviceId = device.Id,
            Device = device,
            SongId = song.Id,
            Song = song,
            DevicePath = devicePath,
            AddedAt = DateTime.UtcNow,
        };
        db.Add(sd);
        db.SaveChanges();
        return sd;
    }

    [Fact]
    public async Task UploadAsync_NewFile_NoDuplicate_CreatesCreateRemoteRecord()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var fileStream = new MemoryStream(fileContent);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.CreateRemote);
        result.EffectiveSongId.ShouldBeNull();
    }

    [Fact]
    public async Task UploadAsync_ExistingDevice_CreatesUpdateRemoteRecord()
    {
        var scenario = new Scenario();
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");
        var songDevice = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var fileStream = new MemoryStream(fileContent);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_DuplicateWithSongIdInLibrary_CreatesLinkRecord()
    {
        var scenario = new Scenario();
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var checksum = ChecksumService.ComputeChecksumFromBytes(content, "XxHash128");
        song.Checksum = checksum;
        scenario.DbContext.SaveChanges();

        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var checksums = call.ArgAt<List<string>>(2);
                if (checksums.Contains(checksum))
                    return new Dictionary<string, Song> { { checksum, song } };
                return new Dictionary<string, Song>();
            });

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(content);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_DryRun_CreatesSameRecordTypeAsLive()
    {
        var scenario = new Scenario();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, isDryRun: true, repositoryPath: "/data");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: true,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.CreateRemote);
        result.Record.Data.ShouldNotBeNull();
        var data = result.Record.Data.Value;
        data.TryGetProperty("tempFilePath", out var tempProp).ShouldBeTrue();
        tempProp.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task UploadAsync_DryRun_DoesNotCreateStagingDirectoryInRepo()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, isDryRun: true, repositoryPath: "/data");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: true,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        mockFs.Directory.Exists("/data/.temp").ShouldBeFalse();
    }

    [Fact]
    public async Task UploadAsync_Live_CreatesStagingDirectory()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        mockFs.Directory.Exists($"/data/.temp/sync-{session.Id}").ShouldBeTrue();
    }

    [Fact]
    public async Task UploadAsync_WithDuplicateInSession_LinksToExistingCreateRemoteSongId()
    {
        var scenario = new Scenario();
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id);

        var firstStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var checksum = ChecksumService.ComputeChecksumFromBytes(firstStream.ToArray(), "XxHash128");
        song.Checksum = checksum;
        scenario.DbContext.SaveChanges();

        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var checksums = call.ArgAt<List<string>>(2);
                if (checksums.Contains(checksum))
                    return new Dictionary<string, Song> { { checksum, song } };
                return new Dictionary<string, Song>();
            });

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_UpdateDoesNotDowngradeToLink()
    {
        var scenario = new Scenario();
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");
        var songDevice = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        result.EffectiveSongId.ShouldBe(songDevice.SongId);
    }
}
