using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DevicesControllerCreatePendingActionsSpecs
{
    private const string NamingTemplate = "{{ simple_label }}{{ extension }}";

    private DevicesController CreateController(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var config = Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>();
        config.Value.Returns(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = NamingTemplate
        });

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            config,
            Substitute.For<System.IO.Abstractions.IFileSystem>(),
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ISyncCommitService>(),
            Substitute.For<ISyncUploadService>()
        );
    }

    private static string ComputeExpectedPath(Song song)
    {
        var namingStrategy = new TemplateNamingStrategy(NamingTemplate);
        var metadata = EntityConverter.ToSong(song);
        var naming = NamingMetadata.FromPath(song.RepositoryPath);
        return namingStrategy.Generate(metadata, naming);
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

    private DeviceSyncSession CreateSession(MusicDbContext db, Device device, SyncSessionStatus status)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = status,
            Records = []
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private Song CreateSong(MusicDbContext db)
    {
        var ownerId = db.Users.First().Id;

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
            Artist = artist,
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
            Album = album,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            RepositoryPath = "/music/song.mp3",
            Checksum = "abc123",
            ChecksumAlgorithm = "SHA256",
            Size = 1000,
            Duration = TimeSpan.FromSeconds(180),
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Artists =
            [
                new SongArtist { ArtistId = artist.Id, Artist = artist }
            ],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        db.Songs.Add(song);
        db.SaveChanges();
        return song;
    }

    private SongDevice CreateSongDevice(MusicDbContext db, Device device, Song? song, string path, SongSyncAction? syncAction = null)
    {
        var songDevice = new SongDevice
        {
            DeviceId = device.Id,
            Device = device,
            DevicePath = path,
            SongId = song?.Id,
            Song = song,
            SyncAction = syncAction,
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        return songDevice;
    }

    [Fact]
    public async Task CreatePendingActions_DownloadSyncAction_CreatesCreateLocalRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        CreateSongDevice(scenario.DbContext, device, song, expectedPath, SongSyncAction.Download);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        response.Value.Records[0].FilePath.ShouldBe(expectedPath);
        response.Value.Records[0].SongId.ShouldBe(song.Id);
        response.Value.Records[0].Acknowledged.ShouldBeFalse();

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        records[0].Acknowledged.ShouldBeFalse();
    }

    [Fact]
    public async Task CreatePendingActions_RemoveSyncAction_CreatesUnlinkRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        CreateSongDevice(scenario.DbContext, device, song, expectedPath, SongSyncAction.Remove);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.Unlink);
        response.Value.Records[0].FilePath.ShouldBe(expectedPath);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.Unlink);
    }

    [Fact]
    public async Task CreatePendingActions_DownloadWithPreviousSync_CreatesUpdateLocalRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        var songDevice = CreateSongDevice(scenario.DbContext, device, song, expectedPath, SongSyncAction.Download);
        songDevice.LastSyncedModifiedAt = DateTime.UtcNow;
        scenario.DbContext.SaveChanges();

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.UpdateLocal);
    }

    [Fact]
    public async Task CreatePendingActions_PathChanged_CreatesOnlyCreateLocalRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        CreateSongDevice(scenario.DbContext, device, song, "OldPath.mp3", SongSyncAction.Download);
        var expectedNewPath = ComputeExpectedPath(song);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.ShouldNotBeNull();
        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        response.Value.Records[0].FilePath.ShouldBe(expectedNewPath);
    }

    [Fact]
    public async Task CreatePendingActions_CalledTwice_DoesNotCreateDuplicateRecords()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        CreateSongDevice(scenario.DbContext, device, song, expectedPath, SongSyncAction.Download);

        await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);
        await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreatePendingActions_UploadSyncAction_CreatesNoRecords()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var song = CreateSong(scenario.DbContext);
        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        CreateSongDevice(scenario.DbContext, device, song, expectedPath, SongSyncAction.Upload);

        var response = await controller.CreatePendingActions(device.Id, session.Id, CancellationToken.None);

        response.Value.Records.ShouldBeEmpty();
    }
}