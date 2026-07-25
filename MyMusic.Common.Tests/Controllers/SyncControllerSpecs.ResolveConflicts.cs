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

public class SyncControllerResolveConflictsSpecs
{
    private SyncController CreateController(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new SyncController(
            Substitute.For<ILogger<SyncController>>(),
            currentUser,
            scenario.DbContext,
            scenario.FileSystem,
            SyncControllerHelpers.CreateSyncStartService(scenario),
            SyncControllerHelpers.CreateSyncCompleteService(scenario),
            SyncControllerHelpers.CreateSyncCancelService(scenario),
            Substitute.For<ISyncCommitService>(),
            SyncControllerHelpers.CreateSyncPendingActionsService(scenario),
            SyncControllerHelpers.CreateSyncDeviceSongsService(scenario),
            SyncControllerHelpers.CreateSyncCheckService(scenario),
            SyncControllerHelpers.CreateSyncResolveConflictsService(scenario, factory),
            Substitute.For<ISyncReportErrorService>(),
            Substitute.For<ISyncAcknowledgeService>(),
            DevicesControllerHelpers.SessionLookup);
    }

    private Song CreateSongWithChecksum(MusicDbContext db, long ownerId, byte[] content, string checksumAlgorithm = "XxHash128")
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
            throw new ArgumentException($"Unknown checksum algorithm: {checksumAlgorithm}", nameof(checksumAlgorithm));
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


    [Fact]
    public async Task ResolveConflicts_ChecksumsMatch_NoActiveSession_ReturnsNotFound()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice();

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, content);
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

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
        var device = scenario.CreateDevice();

        var content = new byte[] { 10, 20, 30 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, content);
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

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
        var device = scenario.CreateDevice();

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, serverContent);
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

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
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
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
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

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
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
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
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

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
        var device = scenario.CreateDevice();

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = CreateSongWithChecksum(scenario.DbContext, scenario.AdminUser.Id, serverContent);
        var sd = scenario.CreateSongDevice(device, song, "/music/song.mp3",
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

    [Fact]
    public async Task ResolveConflicts_ChecksumsMatch_NewLastSyncedUsesFileModifiedAtNotModifiedAt()
    {
        // When checksums match (no file content change), the server resolves by updating
        // LastSyncedModifiedAt to max(local, FileModifiedAt). Since FileModifiedAt reflects
        // the last file-content change (not metadata edits), a metadata-only edit that bumped
        // ModifiedAt newer than FileModifiedAt must NOT inflate the new LastSyncedModifiedAt.
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var controller = CreateController(scenario, factory);
        var device = scenario.CreateDevice();
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
        // FileModifiedAt is OLDER than ModifiedAt (metadata edit happened after last file change).
        var fileModifiedAt = DateTime.UtcNow.AddHours(-3);
        var modifiedAt = DateTime.UtcNow.AddHours(-1);
        song.ModifiedAt = modifiedAt;
        song.FileModifiedAt = fileModifiedAt;
        scenario.DbContext.SaveChanges();

        // Local file mtime is older than FileModifiedAt -> new LastSynced should be FileModifiedAt (the max).
        var localModifiedAt = fileModifiedAt.AddMinutes(-30);
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

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

        await controller.ResolveConflicts(device.Id, session.Id, request, CancellationToken.None);

        var tsRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.UpdateTimestamp)
            .ToListAsync();
        tsRecords.Count.ShouldBe(1);

        // The new LastSynced timestamp must be max(local, FileModifiedAt) = fileModifiedAt,
        // NOT max(local, ModifiedAt) = modifiedAt. This is the core behavior change: a
        // metadata-only edit (ModifiedAt bumped) must NOT inflate the new sync timestamp.
        var tsData = System.Text.Json.JsonSerializer.Deserialize<UpdateTimestampData>(tsRecords[0].Data!.Value, System.Text.Json.JsonSerializerOptions.Default);
        tsData.ShouldNotBeNull();
        tsData.NewTimestamp.ShouldBe(fileModifiedAt, "NewLastSynced should be max(local, FileModifiedAt), not the newer ModifiedAt");
        tsData.NewTimestamp.ShouldNotBe(modifiedAt, "NewLastSynced must NOT use the metadata-bumped ModifiedAt");
    }
}
