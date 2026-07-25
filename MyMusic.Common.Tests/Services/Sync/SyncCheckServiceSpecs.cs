using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncCheckServiceSpecs
{
    private static SyncCheckService CreateService(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var config = Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = "{{ simple_label }}{{ extension }}",
        });
        return new SyncCheckService(
            scenario.DbContext,
            new DeviceLookupService(),
            new SyncSessionLookupService(),
            factory ?? new SyncActionsServerFactory(),
            new SyncPathResolver(),
            new SyncComparisonHelper(),
            config,
            Substitute.For<ILogger<SyncCheckService>>());
    }

    private static SyncCheckInput InputFor(string path, DateTime modifiedAt, bool force = false) =>
        new()
        {
            Files = [new SyncCheckFileInfo { Path = path, ModifiedAt = modifiedAt, CreatedAt = DateTime.UtcNow }],
            Force = force,
        };

    [Fact]
    public async Task CheckAsync_DeviceNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var session = scenario.CreateSession(scenario.CreateDevice(), status: SyncSessionStatus.InProgress);

        // Act
        var result = await service.CheckAsync(9999, session.Id, scenario.AdminUser.Id, InputFor("/x.mp3", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        // Act
        var result = await service.CheckAsync(otherDevice.Id, session.Id, scenario.AdminUser.Id, InputFor("/x.mp3", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var service = CreateService(scenario);

        // Act
        var result = await service.CheckAsync(device.Id, 0, scenario.AdminUser.Id, InputFor("/x.mp3", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_SessionNotInProgress_Throws()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var service = CreateService(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/x.mp3", DateTime.UtcNow), CancellationToken.None));
    }

    [Fact]
    public async Task CheckAsync_NoMatchingSongDevice_ReturnsCreateRemoteRecord_Tentative()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/new.mp3", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.CreateRemote);
        record.FilePath.ShouldBe("/music/new.mp3");
        record.SongId.ShouldBeNull();

        // CreateRemote is tentative (inline) and should not be persisted.
        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        dbRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckAsync_ServerNewerClientUnchanged_ReturnsUpdateLocalRecord_Tentative()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = scenario.CreateSong("Song", modifiedAt: serverModified);
        scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", lastSynced), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateLocal);
        record.SongId.ShouldBe(song.Id);
        record.FilePath.ShouldBe("/music/song.mp3");

        // UpdateLocal is tentative (inline) and should not be persisted.
        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.UpdateLocal)
            .ToListAsync();
        dbRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckAsync_BothUnchanged_ReturnsSkippedRecord_Persisted()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var lastSynced = DateTime.UtcNow.AddHours(-1);
        var serverModified = lastSynced.AddMinutes(-10);
        var song = scenario.CreateSong("Song", modifiedAt: serverModified);
        scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", lastSynced.AddMinutes(-5)), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Skipped);
        record.SongId.ShouldBe(song.Id);

        // Skipped is persisted via the sync actions server.
        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.Skipped)
            .ToListAsync();
        dbRecords.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CheckAsync_ForceFlag_ReturnsUpdateRemoteRecord_Tentative()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var lastSynced = DateTime.UtcNow.AddHours(-1);
        var serverModified = lastSynced.AddMinutes(-30);
        var song = scenario.CreateSong("Song", modifiedAt: serverModified);
        scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: SongSyncAction.Download);

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", lastSynced, force: true), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        record.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task CheckAsync_BothServerAndClientNewer_ReturnsConflictRecord_Tentative()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var lastSynced = DateTime.UtcNow.AddHours(-3);
        var serverModified = DateTime.UtcNow.AddHours(-1);
        var song = scenario.CreateSong("Song", modifiedAt: serverModified);
        scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", DateTime.UtcNow.AddHours(-2)), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Conflict);
        record.SongId.ShouldBe(song.Id);

        // Conflict in the client-newer branch is tentative (inline) and should not be persisted.
        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.Conflict)
            .ToListAsync();
        dbRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckAsync_SyncActionRemove_ReturnsDeleteLocalRecord_Persisted()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var lastSynced = DateTime.UtcNow.AddHours(-1);
        var serverModified = lastSynced.AddMinutes(-10);
        var song = scenario.CreateSong("Song", modifiedAt: serverModified);
        scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: SongSyncAction.Remove);

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", lastSynced.AddMinutes(-5)), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.DeleteLocal);
        record.SongId.ShouldBe(song.Id);

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.DeleteLocal)
            .ToListAsync();
        dbRecords.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CheckAsync_ClientNewerServerUnchanged_ReturnsUpdateRemoteRecord_Tentative()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var serverModified = DateTime.UtcNow.AddHours(-3);
        var song = scenario.CreateSong("Song", modifiedAt: serverModified);
        scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", DateTime.UtcNow.AddHours(-1)), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        record.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task CheckAsync_NoSyncTimestampDownloadActionServerNewer_ReturnsConflictRecord_Persisted()
    {
        // Arrange - no LastSyncedModifiedAt, Download sync action, server newer than AddedAt.
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var song = scenario.CreateSong("Song", modifiedAt: DateTime.UtcNow.AddHours(1));
        var sd = scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: null, syncAction: SongSyncAction.Download);
        // Make AddedAt older so the server is "newer than added".
        sd.AddedAt = DateTime.UtcNow.AddHours(-2);
        scenario.DbContext.SaveChanges();

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Conflict);
        record.SongId.ShouldBe(song.Id);

        // This conflict branch uses syncActions.ActionConflict which persists.
        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.Conflict)
            .ToListAsync();
        dbRecords.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CheckAsync_NoSyncTimestampDownloadActionServerNotNewer_ReturnsUpdateRemoteRecord_Tentative()
    {
        // Arrange - no LastSyncedModifiedAt, Download sync action, server NOT newer than AddedAt.
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var song = scenario.CreateSong("Song", modifiedAt: DateTime.UtcNow.AddHours(-3));
        var sd = scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: null, syncAction: SongSyncAction.Download);
        sd.AddedAt = DateTime.UtcNow;
        scenario.DbContext.SaveChanges();

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        record.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task CheckAsync_NoSyncTimestampNotDownload_ReturnsUpdateRemoteRecord_Tentative()
    {
        // Arrange - no LastSyncedModifiedAt, no Download sync action.
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: null, syncAction: null);

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor("/music/song.mp3", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        record.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task CheckAsync_MultipleFiles_ProducesOneRecordPerFile()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var lastSynced = DateTime.UtcNow.AddHours(-1);
        var song1 = scenario.CreateSong("Song1", modifiedAt: lastSynced.AddMinutes(-10));
        var song2 = scenario.CreateSong("Song2", modifiedAt: lastSynced.AddMinutes(-10));
        scenario.CreateSongDevice(device, song1, "/music/song1.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);
        scenario.CreateSongDevice(device, song2, "/music/song2.mp3",
            lastSyncedModifiedAt: lastSynced, syncAction: null);

        var input = new SyncCheckInput
        {
            Files =
            [
                new SyncCheckFileInfo { Path = "/music/song1.mp3", ModifiedAt = lastSynced.AddMinutes(-5), CreatedAt = DateTime.UtcNow },
                new SyncCheckFileInfo { Path = "/music/song2.mp3", ModifiedAt = lastSynced.AddMinutes(-5), CreatedAt = DateTime.UtcNow },
                new SyncCheckFileInfo { Path = "/music/new.mp3", ModifiedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            ],
            Force = false,
        };

        // Act
        var result = await service.CheckAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(3);
        result.Records.ShouldContain(r => r.Action == SyncRecordAction.Skipped && r.FilePath == "/music/song1.mp3");
        result.Records.ShouldContain(r => r.Action == SyncRecordAction.Skipped && r.FilePath == "/music/song2.mp3");
        result.Records.ShouldContain(r => r.Action == SyncRecordAction.CreateRemote && r.FilePath == "/music/new.mp3");
    }
}
