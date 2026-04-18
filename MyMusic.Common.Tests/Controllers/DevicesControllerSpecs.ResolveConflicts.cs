using System.IO.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
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
            Substitute.For<ILogger<MusicImportJob>>(),
            Substitute.For<System.IO.Abstractions.IFileSystem>()
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
    public async Task ResolveConflicts_ChecksumsMatch_ReturnsInResolvedNotToUpload()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, content);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

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
            ]
        };

        // Act
        var response = await controller.ResolveConflicts(device.Id, request, CancellationToken.None);

        // Assert
        response.ToUpload.ShouldBeEmpty();
        response.Conflicts.ShouldBeEmpty();
        response.Resolved.Count.ShouldBe(1);
        response.Resolved[0].Path.ShouldBe("/music/song.mp3");
    }

    [Fact]
    public async Task ResolveConflicts_ChecksumsMatch_AdvancesLastSyncedModifiedAt()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var content = new byte[] { 10, 20, 30 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, content);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

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
            ]
        };

        // Act
        await controller.ResolveConflicts(device.Id, request, CancellationToken.None);

        // Assert
        var updatedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        var expected = localModifiedAt > song.ModifiedAt ? localModifiedAt : song.ModifiedAt;
        updatedSd.LastSyncedModifiedAt.ShouldNotBeNull();
        ((DateTime)updatedSd.LastSyncedModifiedAt).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ResolveConflicts_ChecksumsDiffer_ReturnsInConflicts()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, serverContent);
        var sd = CreateSongDevice(scenario.DbContext, device, song, "/music/song.mp3");

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
            ]
        };

        // Act
        var response = await controller.ResolveConflicts(device.Id, request, CancellationToken.None);

        // Assert
        response.ToUpload.ShouldBeEmpty();
        response.Resolved.ShouldBeEmpty();
        response.Conflicts.Count.ShouldBe(1);
        response.Conflicts[0].Path.ShouldBe("/music/song.mp3");
        response.Conflicts[0].Reason.ShouldContain("Checksum mismatch");
    }
}