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
    private DevicesController CreateController(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var config = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
        config["MyMusic:MusicRepositoryPath"].Returns("/data");

        var syncUploadService = new SyncUploadService(
            scenario.DbContext,
            scenario.FileSystem,
            scenario.CreateMusicService(),
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ILogger<SyncUploadService>>());

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            config,
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            scenario.FileSystem,
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ISyncCommitService>(),
            syncUploadService
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

    private Song CreateSong(MusicDbContext db, long ownerId)
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
        formFile.OpenReadStream().Returns(new MemoryStream(content));
        return formFile;
    }

    [Fact]
    public async Task UploadFile_NewFile_SetsIsUpdateFalseAndMapsCreateRemoteResponse()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var formFile = CreateMockFormFile(new byte[] { 1, 2, 3, 4, 5 });
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();
        response.Value.Action.ShouldBe(SyncRecordAction.CreateRemote.ToString());
        response.Value.SongId.ShouldBeNull();
        response.Value.RecordId.ShouldNotBeNull();
    }

    [Fact]
    public async Task UploadFile_ExistingDevice_SetsIsUpdateTrueAndMapsUpdateRemoteResponse()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var song = CreateSong(scenario.DbContext, scenario.AdminUser.Id);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = CreateSession(scenario.DbContext, device, repositoryPath: "/data");
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var controller = CreateController(scenario, factory);
        var formFile = CreateMockFormFile(new byte[] { 1, 2, 3, 4, 5 });
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();
        response.Value.Action.ShouldBe(SyncRecordAction.UpdateRemote.ToString());
        response.Value.SongId.ShouldBe(song.Id);
        response.Value.RecordId.ShouldNotBeNull();
    }
}