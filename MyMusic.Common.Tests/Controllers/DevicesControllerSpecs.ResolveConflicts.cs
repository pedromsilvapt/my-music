using System.IO.Hashing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DevicesControllerResolveConflictsSpecs
{
    private DevicesController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            Substitute.For<IMusicService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            Substitute.For<System.IO.Abstractions.IFileSystem>(),
            Substitute.For<ISyncActionsServerFactory>(),
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

    private Song CreateSongWithChecksum(MusicDbContext db, long ownerId, byte[] content, string checksumAlgorithm = "MD5")
    {
        string checksum;
        if (checksumAlgorithm == "XxHash128")
        {
            var xxHash = new XxHash128();
            xxHash.Append(content);
            checksum = Convert.ToBase64String(xxHash.GetCurrentHash());
        }
        else
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            checksum = Convert.ToBase64String(md5.ComputeHash(content));
        }

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
            Checksum = checksum,
            ChecksumAlgorithm = checksumAlgorithm,
            Size = content.Length,
            Duration = TimeSpan.FromSeconds(180),
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow.AddHours(-1),
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        db.Add(song);
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

    [Fact]
    public async Task ResolveConflicts_ChecksumsMatch_NoActiveSession_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, content);
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var localModifiedAt = DateTime.UtcNow;
        var request = new SyncResolveConflictsRequest
        {
            Conflicts =
            [
                new SyncConflictResolveItem
                {
                    Path = "/music/song.mp3",
                    SongId = song.Id,
                    FileContentBase64 = Convert.ToBase64String(content),
                    LocalModifiedAt = localModifiedAt,
                }
            ],
            PotentialUpdates = []
        };

        var response = await controller.ResolveConflicts(device.Id, 0, request, CancellationToken.None);

        response.Result.ShouldNotBeNull();
        response.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ResolveConflicts_ChecksumsMatch_NoActiveSession2_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var content = new byte[] { 10, 20, 30 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, content);
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var localModifiedAt = DateTime.UtcNow.AddMinutes(5);
        var request = new SyncResolveConflictsRequest
        {
            Conflicts =
            [
                new SyncConflictResolveItem
                {
                    Path = "/music/song.mp3",
                    SongId = song.Id,
                    FileContentBase64 = Convert.ToBase64String(content),
                    LocalModifiedAt = localModifiedAt,
                }
            ],
            PotentialUpdates = []
        };

        var response = await controller.ResolveConflicts(device.Id, 0, request, CancellationToken.None);

        response.Result.ShouldNotBeNull();
        response.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ResolveConflicts_ChecksumsDiffer_NoActiveSession_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, serverContent);
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var request = new SyncResolveConflictsRequest
        {
            Conflicts =
            [
                new SyncConflictResolveItem
                {
                    Path = "/music/song.mp3",
                    SongId = song.Id,
                    FileContentBase64 = Convert.ToBase64String(clientContent),
                    LocalModifiedAt = DateTime.UtcNow,
                }
            ],
            PotentialUpdates = []
        };

        var response = await controller.ResolveConflicts(device.Id, 0, request, CancellationToken.None);

        response.Result.ShouldNotBeNull();
        response.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ResolveConflicts_InvalidBase64_CreatesErrorRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var currentUser = Substitute.For<MyMusic.Common.Services.ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var controller = new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            Substitute.For<IMusicService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            Substitute.For<System.IO.Abstractions.IFileSystem>(),
            factory,
            Substitute.For<ISyncCommitService>()
        );
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = false,
            Records = []
        };
        scenario.DbContext.DeviceSyncSessions.Add(session);
        scenario.DbContext.SaveChanges();

        var content = new byte[] { 1, 2, 3 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, content);
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var request = new SyncResolveConflictsRequest
        {
            Conflicts =
            [
                new SyncConflictResolveItem
                {
                    Path = "/music/song.mp3",
                    SongId = song.Id,
                    FileContentBase64 = "not-valid-base64!!!",
                    LocalModifiedAt = DateTime.UtcNow,
                }
            ],
            PotentialUpdates = []
        };

        var response = await controller.ResolveConflicts(device.Id, session.Id, request, CancellationToken.None);

        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.Error);
        response.Value.Records[0].Reason.ShouldBe("Invalid file content format");

        var errorRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.Error)
            .ToListAsync();
        errorRecords.Count.ShouldBe(1);
        errorRecords[0].FilePath.ShouldBe("/music/song.mp3");
    }

    [Fact]
    public async Task ResolveConflicts_ChecksumsMatch_CreatesUpdateTimestampRecord()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var currentUser = Substitute.For<MyMusic.Common.Services.ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var controller = new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            Substitute.For<IMusicService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            Substitute.For<System.IO.Abstractions.IFileSystem>(),
            factory,
            Substitute.For<ISyncCommitService>()
        );
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = false,
            Records = []
        };
        scenario.DbContext.DeviceSyncSessions.Add(session);
        scenario.DbContext.SaveChanges();

        var content = new byte[] { 10, 20, 30 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, content);
        CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

        var localModifiedAt = DateTime.UtcNow;
        var request = new SyncResolveConflictsRequest
        {
            Conflicts =
            [
                new SyncConflictResolveItem
                {
                    Path = "/music/song.mp3",
                    SongId = song.Id,
                    FileContentBase64 = Convert.ToBase64String(content),
                    LocalModifiedAt = localModifiedAt,
                }
            ],
            PotentialUpdates = []
        };

        var response = await controller.ResolveConflicts(device.Id, session.Id, request, CancellationToken.None);

        response.Value.Records.Count.ShouldBe(1);
        response.Value.Records[0].Action.ShouldBe(SyncRecordAction.UpdateTimestamp);

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records.ShouldContain(r => r.Action == SyncRecordAction.UpdateTimestamp);
    }

    [Fact]
    public async Task ResolveConflicts_ChecksumsDiffer_DoesNotMutateLastSyncedModifiedAt()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, serverContent);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3",
            lastSyncedModifiedAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var request = new SyncResolveConflictsRequest
        {
            Conflicts =
            [
                new SyncConflictResolveItem
                {
                    Path = "/music/song.mp3",
                    SongId = song.Id,
                    FileContentBase64 = Convert.ToBase64String(clientContent),
                    LocalModifiedAt = DateTime.UtcNow,
                }
            ],
            PotentialUpdates = []
        };

        await controller.ResolveConflicts(device.Id, 0, request, CancellationToken.None);

        var unchangedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        unchangedSd.LastSyncedModifiedAt.ShouldBe(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}