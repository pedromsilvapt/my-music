using System.IO.Abstractions.TestingHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DevicesControllerUploadSpecs
{
    private readonly IMusicService _musicService = Substitute.For<IMusicService>();

    public DevicesControllerUploadSpecs()
    {
        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Song>());
    }

    private DevicesController CreateController(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var config = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
        config["MyMusic:MusicRepositoryPath"].Returns("/data");

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            _musicService,
            config,
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            scenario.FileSystem,
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ISyncCommitService>()
        );
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

    private Song CreateSong(MusicDbContext db, long ownerId, string? repositoryPath = null)
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
            RepositoryPath = repositoryPath ?? "/music/song.mp3",
            Checksum = "abc123",
            ChecksumAlgorithm = "XxHash128",
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

    private SongDevice CreateSongDevice(MusicDbContext db, Device device, Song song, string devicePath,
        DateTime? lastSyncedModifiedAt = null)
    {
        var sd = new SongDevice
        {
            DeviceId = device.Id,
            Device = device,
            SongId = song.Id,
            Song = song,
            DevicePath = devicePath,
            AddedAt = DateTime.UtcNow,
            LastSyncedModifiedAt = lastSyncedModifiedAt,
        };
        db.Add(sd);
        db.SaveChanges();
        return sd;
    }

    private static IFormFile CreateMockFormFile(byte[] content, string fileName = "song.mp3")
    {
        var formFile = Substitute.For<IFormFile>();
        formFile.FileName.Returns(fileName);
        formFile.CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var stream = call.ArgAt<Stream>(0);
                await stream.WriteAsync(content);
            });
        return formFile;
    }

    [Fact]
    public async Task UploadFile_Staged_SavesToStagingDirectory()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();
        response.Value.RecordId.ShouldNotBeNull();
        response.Value.Action.ShouldNotBeNull();

        var stagingDir = $"/data/.temp/sync-{session.Id}";
        mockFs.Directory.Exists(stagingDir).ShouldBeTrue();
    }

    [Fact]
    public async Task UploadFile_Staged_DataContainsTempFilePath()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Data.ShouldNotBeNull();
        var data = response.Value.Data.Value;
        data.TryGetProperty("tempFilePath", out var tempProp).ShouldBeTrue();
        tempProp.GetString().ShouldNotBeNull();
        tempProp.GetString()!.ShouldContain($"sync-");
    }

    [Fact]
    public async Task UploadFile_Staged_CreatesSessionRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBeOneOf(SyncRecordAction.CreateRemote, SyncRecordAction.UpdateRemote);
    }

    [Fact]
    public async Task UploadFile_Staged_ExistingDevice_CreatesUpdateRemoteRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Action.ShouldBe(SyncRecordAction.UpdateRemote.ToString());
        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.UpdateRemote);
    }

    [Fact]
    public async Task UploadFile_Staged_NewFile_CreatesCreateRemoteRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Action.ShouldBe(SyncRecordAction.CreateRemote.ToString());
    }

    [Fact]
    public async Task UploadFile_Staged_NewFile_SongIdIsNull()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.SongId.ShouldBeNull();
    }

    [Fact]
    public async Task UploadFile_Staged_ExistingDevice_SongIdIsSet()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadFile_Staged_DryRun_SkipsStagedFileSaving()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, isDryRun: true, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();
        response.Value.Data.ShouldNotBeNull();
        var data = response.Value.Data.Value;
        data.TryGetProperty("tempFilePath", out var tempProp).ShouldBeTrue();
        tempProp.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task UploadFile_Staged_DryRun_DoesNotCreateStagingDirectoryInRepo()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, isDryRun: true, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        mockFs.Directory.Exists("/data/.temp").ShouldBeFalse();
    }

    [Fact]
    public async Task UploadFile_Staged_DryRun_SongIdIsNull()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, isDryRun: true, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.SongId.ShouldBeNull();
    }

    [Fact]
    public async Task UploadFile_DoesNotUpdateDeviceLastSyncAt()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");
        device.LastSyncAt = null;
        scenario.DbContext.SaveChanges();

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        var updatedDevice = await scenario.DbContext.Devices.FindAsync([device.Id]);
        updatedDevice!.LastSyncAt.ShouldBeNull();
    }

    [Fact]
    public async Task UploadFile_DryRun_DoesNotUpdateDeviceLastSyncAt()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, isDryRun: true, repositoryPath: "/data");
        device.LastSyncAt = null;
        scenario.DbContext.SaveChanges();

        var controller = CreateController(scenario, factory);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var formFile = CreateMockFormFile(fileContent);
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        var updatedDevice = await scenario.DbContext.Devices.FindAsync([device.Id]);
        updatedDevice!.LastSyncAt.ShouldBeNull();
    }
}