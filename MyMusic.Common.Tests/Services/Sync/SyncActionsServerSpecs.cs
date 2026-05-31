using System.Text.Json;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncActionsServerSpecs
{
    private (MusicDbContext db, Device device, DeviceSyncSession session, User user, SyncActionsServer server) Setup()
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var device = scenario.CreateDevice("Phone", namingTemplate: "/music/{Artist}/{Album}/{Title}");
        var session = scenario.CreateSession(device);
        var server = new SyncActionsServer(db, session.Id);
        return (db, device, session, user, server);
    }

    private (MusicDbContext db, Device device, DeviceSyncSession session, User user, Song song, SyncActionsServer server) SetupWithSong()
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);
        var device = scenario.CreateDevice("Phone", namingTemplate: "/music/{Artist}/{Album}/{Title}");
        var session = scenario.CreateSession(device);
        var server = new SyncActionsServer(db, session.Id);
        return (db, device, session, user, song, server);
    }

    #region ActionCreateRemote

    [Fact]
    public async Task ActionCreateRemote_CreatesRecord_WithCorrectActionAndEnrichedData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2025, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var tempFilePath = "/tmp/mymusic_import_abc123/song.mp3";

        var record = await server.ActionCreateRemote("/music/song.mp3", song.Id, "abc123", "XxHash128", modifiedAt, tempFilePath, createdAt);

        record.Action.ShouldBe(SyncRecordAction.CreateRemote);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.SongId.ShouldBe(song.Id);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
        data.GetProperty("checksum").GetString().ShouldBe("abc123");
        data.GetProperty("algorithm").GetString().ShouldBe("XxHash128");
        data.GetProperty("modifiedAt").GetString().ShouldBe(modifiedAt.ToString("O"));
        data.GetProperty("tempFilePath").GetString().ShouldBe(tempFilePath);
        data.GetProperty("createdAt").GetString().ShouldBe(createdAt.ToString("O"));
    }

    [Fact]
    public async Task ActionCreateRemote_WithOptionalDefaults_StoresNullsInData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionCreateRemote("/music/song.mp3", song.Id, "abc123", "XxHash128", modifiedAt);

        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("modifiedAt").GetString().ShouldBe(modifiedAt.ToString("O"));
        data.TryGetProperty("tempFilePath", out var tempProp).ShouldBeTrue();
        tempProp.ValueKind.ShouldBe(JsonValueKind.Null);
        data.TryGetProperty("createdAt", out var createdProp).ShouldBeTrue();
        createdProp.ValueKind.ShouldBe(JsonValueKind.Null);
        data.TryGetProperty("originalFilePath", out var originalProp).ShouldBeTrue();
        originalProp.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ActionCreateRemote_WithOriginalFilePath_StoresInData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var tempFilePath = "/tmp/mymusic_import_abc123/song.mp3";
        var originalFilePath = "/tmp/mymusic_staging/song.mp3";

        var record = await server.ActionCreateRemote("/music/song.mp3", song.Id, "abc123", "XxHash128", modifiedAt, tempFilePath, null, originalFilePath);

        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("tempFilePath").GetString().ShouldBe(tempFilePath);
        data.GetProperty("originalFilePath").GetString().ShouldBe(originalFilePath);
    }

    [Fact]
    public async Task ActionCreateRemote_DoesNotPerformSideEffects()
    {
        var (db, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var initialCount = db.SongDevices.Count();

        await server.ActionCreateRemote("/music/song.mp3", song.Id, "abc123", "XxHash128", modifiedAt);

        db.SongDevices.Count().ShouldBe(initialCount);
    }

    [Fact]
    public async Task ActionCreateRemote_NullSongId_StoresNullInData()
    {
        var (_, _, _, _, server) = Setup();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionCreateRemote("/music/song.mp3", (long?)null, "abc123", "XxHash128", modifiedAt);

        record.SongId.ShouldBeNull();
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.TryGetProperty("songId", out var songIdProp).ShouldBeTrue();
        songIdProp.ValueKind.ShouldBe(JsonValueKind.Null);
        data.GetProperty("checksum").GetString().ShouldBe("abc123");
    }

    #endregion

    #region ActionUpdateRemote

    [Fact]
    public async Task ActionUpdateRemote_CreatesRecord_WithCorrectActionAndEnrichedData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2025, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var tempFilePath = "/tmp/mymusic_import_def456/song.mp3";

        var record = await server.ActionUpdateRemote("/music/song.mp3", song.Id, "def456", "XxHash128", modifiedAt, tempFilePath, createdAt);

        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.SongId.ShouldBe(song.Id);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
        data.GetProperty("checksum").GetString().ShouldBe("def456");
        data.GetProperty("algorithm").GetString().ShouldBe("XxHash128");
        data.GetProperty("modifiedAt").GetString().ShouldBe(modifiedAt.ToString("O"));
        data.GetProperty("tempFilePath").GetString().ShouldBe(tempFilePath);
        data.GetProperty("createdAt").GetString().ShouldBe(createdAt.ToString("O"));
    }

    [Fact]
    public async Task ActionUpdateRemote_DoesNotUpdateLastSyncedModifiedAt()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionUpdateRemote("/music/song.mp3", song.Id, "def456", "XxHash128", modifiedAt);

        var unchanged = db.SongDevices.First(sd => sd.Id == songDeviceId);
        unchanged.LastSyncedModifiedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ActionUpdateRemote_WithOriginalFilePath_StoresInData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var tempFilePath = "/tmp/mymusic_import_def456/song.mp3";
        var originalFilePath = "/tmp/mymusic_staging/song.mp3";

        var record = await server.ActionUpdateRemote("/music/song.mp3", song.Id, "def456", "XxHash128", modifiedAt, tempFilePath, null, originalFilePath);

        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("tempFilePath").GetString().ShouldBe(tempFilePath);
        data.GetProperty("originalFilePath").GetString().ShouldBe(originalFilePath);
    }

    #endregion

    #region ActionCreateLocal

    [Fact]
    public async Task ActionCreateLocal_CreatesRecord_WithCorrectActionAndData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionCreateLocal("/music/song.mp3", song.Id, modifiedAt);

        record.Action.ShouldBe(SyncRecordAction.CreateLocal);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.SongId.ShouldBe(song.Id);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
        data.GetProperty("modifiedAt").GetString().ShouldBe(modifiedAt.ToString("O"));
    }

    [Fact]
    public async Task ActionCreateLocal_WithoutOptionalParams_CreatesRecordWithSerializedData()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionCreateLocal("/music/song.mp3");

        record.Action.ShouldBe(SyncRecordAction.CreateLocal);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.TryGetProperty("songId", out var songIdProp).ShouldBeTrue();
        songIdProp.ValueKind.ShouldBe(JsonValueKind.Null);
        data.TryGetProperty("modifiedAt", out var modifiedAtProp).ShouldBeTrue();
        modifiedAtProp.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    #endregion

    #region ActionUpdateLocal

    [Fact]
    public async Task ActionUpdateLocal_CreatesRecord_WithCorrectActionAndData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionUpdateLocal("/music/song.mp3", song.Id, modifiedAt);

        record.Action.ShouldBe(SyncRecordAction.UpdateLocal);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.SongId.ShouldBe(song.Id);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
        data.GetProperty("modifiedAt").GetString().ShouldBe(modifiedAt.ToString("O"));
    }

    [Fact]
    public async Task ActionUpdateLocal_WithoutOptionalParams_CreatesRecordWithSerializedData()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionUpdateLocal("/music/song.mp3");

        record.Action.ShouldBe(SyncRecordAction.UpdateLocal);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.TryGetProperty("songId", out var songIdProp).ShouldBeTrue();
        songIdProp.ValueKind.ShouldBe(JsonValueKind.Null);
        data.TryGetProperty("modifiedAt", out var modifiedAtProp).ShouldBeTrue();
        modifiedAtProp.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    #endregion

    #region ActionDelete

    [Fact]
    public async Task ActionDelete_CreatesRecord_WithCorrectActionAndData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();

        var record = await server.ActionDelete("/music/song.mp3", song.Id);

        record.Action.ShouldBe(SyncRecordAction.Delete);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.SongId.ShouldBe(song.Id);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
    }

    [Fact]
    public async Task ActionDelete_WithoutSongId_CreatesRecordWithSerializedData()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionDelete("/music/song.mp3");

        record.Action.ShouldBe(SyncRecordAction.Delete);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.TryGetProperty("songId", out var songIdProp).ShouldBeTrue();
        songIdProp.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ActionDelete_DoesNotRemoveSongDevice()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionDelete("/music/song.mp3", song.Id);

        db.SongDevices.Any(sd => sd.Id == songDeviceId).ShouldBeTrue();
    }

    #endregion

    #region ActionLink

    [Fact]
    public async Task ActionLink_CreatesRecord_WithCorrectActionAndData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionLink("/music/song.mp3", song.Id, modifiedAt);

        record.Action.ShouldBe(SyncRecordAction.Link);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.SongId.ShouldBe(song.Id);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
        data.GetProperty("modifiedAt").GetString().ShouldBe(modifiedAt.ToString("O"));
    }

    [Fact]
    public async Task ActionLink_WithoutModifiedAt_CreatesRecordWithSongIdInData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();

        var record = await server.ActionLink("/music/song.mp3", song.Id);

        record.Action.ShouldBe(SyncRecordAction.Link);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
    }

    [Fact]
    public async Task ActionLink_DoesNotAddSongToDevice()
    {
        var (db, _, _, _, song, server) = SetupWithSong();
        var initialCount = db.SongDevices.Count();

        await server.ActionLink("/music/song.mp3", song.Id);

        db.SongDevices.Count().ShouldBe(initialCount);
    }

    #endregion

    #region ActionUnlink

    [Fact]
    public async Task ActionUnlink_CreatesRecord_WithCorrectActionAndData()
    {
        var (_, _, _, _, song, server) = SetupWithSong();

        var record = await server.ActionUnlink("/music/song.mp3", song.Id);

        record.Action.ShouldBe(SyncRecordAction.Unlink);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.SongId.ShouldBe(song.Id);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
    }

    [Fact]
    public async Task ActionUnlink_WithoutSongId_CreatesRecordWithSerializedData()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionUnlink("/music/song.mp3");

        record.Action.ShouldBe(SyncRecordAction.Unlink);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.TryGetProperty("songId", out var songIdProp).ShouldBeTrue();
        songIdProp.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ActionUnlink_DoesNotRemoveSongDevice()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionUnlink("/music/song.mp3", song.Id);

        db.SongDevices.Any(sd => sd.Id == songDeviceId).ShouldBeTrue();
    }

    #endregion

    #region ActionRename

    [Fact]
    public async Task ActionRename_CreatesRecord_WithCorrectActionAndData()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionRename("/music/new.mp3", "/music/old.mp3", "/music/new.mp3");

        record.Action.ShouldBe(SyncRecordAction.Rename);
        record.FilePath.ShouldBe("/music/new.mp3");
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("previousPath").GetString().ShouldBe("/music/old.mp3");
        data.GetProperty("newPath").GetString().ShouldBe("/music/new.mp3");
    }

    #endregion

    #region ActionSkipped

    [Fact]
    public async Task ActionSkipped_CreatesRecord_WithCorrectActionAndModifiedAt()
    {
        var (_, _, _, _, server) = Setup();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionSkipped("/music/song.mp3", modifiedAt: modifiedAt);

        record.Action.ShouldBe(SyncRecordAction.Skipped);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("modifiedAt").GetString().ShouldBe(modifiedAt.ToString("O"));
    }

    [Fact]
    public async Task ActionSkipped_WithoutModifiedAt_CreatesRecordWithNullData()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionSkipped("/music/song.mp3");

        record.Action.ShouldBe(SyncRecordAction.Skipped);
        record.Data.ShouldBeNull();
    }

    #endregion

    #region ActionConflict

    [Fact]
    public async Task ActionConflict_CreatesRecord_WithCorrectActionAndData()
    {
        var (_, _, _, _, server) = Setup();
        var localModified = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var serverModified = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionConflict("/music/song.mp3", localModified, serverModified);

        record.Action.ShouldBe(SyncRecordAction.Conflict);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("localModifiedAt").GetString().ShouldBe(localModified.ToString("O"));
        data.GetProperty("serverModifiedAt").GetString().ShouldBe(serverModified.ToString("O"));
    }

    #endregion

    #region ActionUpdateTimestamp

    [Fact]
    public async Task ActionUpdateTimestamp_CreatesRecord_WithCorrectActionAndDataWithSongId()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var newTimestamp = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionUpdateTimestamp("/music/song.mp3", newTimestamp, song.Id);

        record.Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.SongId.ShouldBe(song.Id);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("newTimestamp").GetString().ShouldBe(newTimestamp.ToString("O"));
        data.GetProperty("songId").GetInt64().ShouldBe(song.Id);
    }

    #endregion

    #region ActionError

    [Fact]
    public async Task ActionError_CreatesRecord_WithCorrectActionAndData()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionError("/music/song.mp3", "File not found");

        record.Action.ShouldBe(SyncRecordAction.Error);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
        data.GetProperty("errorMessage").GetString().ShouldBe("File not found");
    }

    #endregion

    #region Record Always Created

    [Fact]
    public async Task Record_Created_EvenWithoutOptionalParameters()
    {
        var (_, _, session, _, server) = Setup();

        var record = await server.ActionSkipped("/music/song.mp3");

        record.ShouldNotBeNull();
        record.Id.ShouldBeGreaterThan(0);
        record.Action.ShouldBe(SyncRecordAction.Skipped);
        record.SessionId.ShouldBe(session.Id);
    }

    #endregion

    #region No Side Effects - CreateLocal

    [Fact]
    public async Task ActionCreateLocal_DoesNotModifyExistingSongDevice()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionCreateLocal("/music/song.mp3", song.Id);

        var unchanged = db.SongDevices.First(sd => sd.Id == songDeviceId);
        unchanged.SyncAction.ShouldBeNull();
    }

    #endregion

    #region No Side Effects - UpdateLocal

    [Fact]
    public async Task ActionUpdateLocal_DoesNotModifyExistingSongDevice()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionUpdateLocal("/music/song.mp3", song.Id);

        var unchanged = db.SongDevices.First(sd => sd.Id == songDeviceId);
        unchanged.SyncAction.ShouldBeNull();
    }

    #endregion

    #region No Side Effects - Rename

    [Fact]
    public async Task ActionRename_DoesNotModifyExistingSongDevice()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/old.mp3",
            AddedAt = DateTime.UtcNow,
            SyncAction = SongSyncAction.Upload,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionRename("/music/new.mp3", "/music/old.mp3", "/music/new.mp3");

        var unchanged = db.SongDevices.First(sd => sd.Id == songDeviceId);
        unchanged.DevicePath.ShouldBe("/music/old.mp3");
        unchanged.SyncAction.ShouldBe(SongSyncAction.Upload);
    }

    #endregion

    #region No Side Effects - Skipped

    [Fact]
    public async Task ActionSkipped_DoesNotModifyExistingSongDevice()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionSkipped("/music/song.mp3", modifiedAt: DateTime.UtcNow);

        var unchanged = db.SongDevices.First(sd => sd.Id == songDeviceId);
        unchanged.LastSyncedModifiedAt.ShouldBeNull();
    }

    #endregion

    #region No Side Effects - Conflict

    [Fact]
    public async Task ActionConflict_DoesNotModifyExistingSongDevice()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var originalModifiedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
            LastSyncedModifiedAt = originalModifiedAt,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionConflict("/music/song.mp3", DateTime.UtcNow, DateTime.UtcNow);

        var unchanged = db.SongDevices.First(sd => sd.Id == songDeviceId);
        unchanged.LastSyncedModifiedAt.ShouldBe(originalModifiedAt);
    }

    #endregion

    #region No Side Effects - UpdateTimestamp

    [Fact]
    public async Task ActionUpdateTimestamp_DoesNotModifyExistingSongDevice()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionUpdateTimestamp("/music/song.mp3", DateTime.UtcNow, song.Id);

        var unchanged = db.SongDevices.First(sd => sd.Id == songDeviceId);
        unchanged.LastSyncedModifiedAt.ShouldBeNull();
    }

    #endregion

    #region No Side Effects - CreateRemote

    [Fact]
    public async Task ActionCreateRemote_DoesNotModifyExistingSongDeviceLastSyncedModifiedAt()
    {
        var (db, device, _, _, song, server) = SetupWithSong();
        var originalModifiedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var songDevice = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = "/music/song.mp3",
            AddedAt = DateTime.UtcNow,
            LastSyncedModifiedAt = originalModifiedAt,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        var songDeviceId = songDevice.Id;

        await server.ActionCreateRemote("/music/song.mp3", song.Id, "abc", "XxHash128", DateTime.UtcNow);

        var unchanged = db.SongDevices.First(sd => sd.Id == songDeviceId);
        unchanged.LastSyncedModifiedAt.ShouldBe(originalModifiedAt);
    }

    #endregion

    #region Dry-Run Records Created

    [Fact]
    public async Task DryRun_RecordsCreatedRegardlessOfDryRunFlag()
    {
        var (_, _, session, _, server) = Setup();
        session.IsDryRun = true;

        var record = await server.ActionCreateLocal("/music/song.mp3");

        record.ShouldNotBeNull();
        record.Id.ShouldBeGreaterThan(0);
        record.Action.ShouldBe(SyncRecordAction.CreateLocal);
    }

    #endregion

    #region Reason Field

    [Fact]
    public async Task ActionCreateRemote_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionCreateRemote("/music/song.mp3", song.Id, "abc123", "XxHash128", modifiedAt, reason: "New file uploaded");

        record.Reason.ShouldBe("New file uploaded");
    }

    [Fact]
    public async Task ActionCreateRemote_WithoutReason_ReasonIsNull()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionCreateRemote("/music/song.mp3", song.Id, "abc123", "XxHash128", modifiedAt);

        record.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task ActionUpdateRemote_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionUpdateRemote("/music/song.mp3", song.Id, "def456", "XxHash128", modifiedAt, reason: "File re-uploaded (updated)");

        record.Reason.ShouldBe("File re-uploaded (updated)");
    }

    [Fact]
    public async Task ActionCreateLocal_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionCreateLocal("/music/song.mp3", song.Id, modifiedAt, reason: "Server modification — new to device");

        record.Reason.ShouldBe("Server modification — new to device");
    }

    [Fact]
    public async Task ActionUpdateLocal_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionUpdateLocal("/music/song.mp3", song.Id, modifiedAt, reason: "Server modification — updated since last sync");

        record.Reason.ShouldBe("Server modification — updated since last sync");
    }

    [Fact]
    public async Task ActionLink_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionLink("/music/song.mp3", song.Id, modifiedAt, reason: "Linked to existing song (duplicate checksum)");

        record.Reason.ShouldBe("Linked to existing song (duplicate checksum)");
    }

    [Fact]
    public async Task ActionUnlink_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, song, server) = SetupWithSong();

        var record = await server.ActionUnlink("/music/song.mp3", song.Id, reason: "Song marked for removal");

        record.Reason.ShouldBe("Song marked for removal");
    }

    [Fact]
    public async Task ActionRename_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionRename("/music/new.mp3", "/music/old.mp3", "/music/new.mp3", reason: "Path updated by naming template");

        record.Reason.ShouldBe("Path updated by naming template");
    }

    [Fact]
    public async Task ActionSkipped_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionSkipped("/music/song.mp3", reason: "No changes detected");

        record.Reason.ShouldBe("No changes detected");
    }

    [Fact]
    public async Task ActionConflict_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, server) = Setup();
        var localModified = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var serverModified = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionConflict("/music/song.mp3", localModified, serverModified, reason: "Conflict: local and server both modified, checksums differ");

        record.Reason.ShouldBe("Conflict: local and server both modified, checksums differ");
    }

    [Fact]
    public async Task ActionUpdateTimestamp_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, song, server) = SetupWithSong();
        var newTimestamp = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var record = await server.ActionUpdateTimestamp("/music/song.mp3", newTimestamp, song.Id, reason: "Timestamp update: checksums match, no file change needed");

        record.Reason.ShouldBe("Timestamp update: checksums match, no file change needed");
    }

    [Fact]
    public async Task ActionError_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionError("/music/song.mp3", "File not found", reason: "Scan error: File not found");

        record.Reason.ShouldBe("Scan error: File not found");
    }

    [Fact]
    public async Task ActionError_WithoutReason_ReasonIsNull()
    {
        var (_, _, _, _, server) = Setup();

        var record = await server.ActionError("/music/song.mp3", "File not found");

        record.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task ActionDelete_WithReason_SetsReasonOnRecord()
    {
        var (_, _, _, _, song, server) = SetupWithSong();

        var record = await server.ActionDelete("/music/song.mp3", song.Id, reason: "Song deleted from library");

        record.Reason.ShouldBe("Song deleted from library");
    }

    #endregion
}