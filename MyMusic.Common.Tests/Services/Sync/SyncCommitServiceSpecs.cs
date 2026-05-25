using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Models;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncCommitServiceSpecs
{
    private readonly IMusicService _musicService = Substitute.For<IMusicService>();
    private readonly ILogger<SyncCommitService> _logger = Substitute.For<ILogger<SyncCommitService>>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();

    private static readonly DateTime DefaultModifiedAt = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public SyncCommitServiceSpecs()
    {
        var importJobLogger = Substitute.For<ILogger<MusicImportJob>>();
        _loggerFactory.CreateLogger(typeof(MusicImportJob)).Returns(importJobLogger);
    }

    private record SyncTestContext(
        MusicDbContext Db,
        Device Device,
        DeviceSyncSession Session,
        User User,
        Song? Song,
        SyncCommitService Service,
        MockFileSystem MockFs);

    private SyncTestContext Setup(bool dryRun = false)
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var device = CreateDevice(db, user.Id, "Phone");
        var session = CreateSession(db, device, dryRun);
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);
        return new SyncTestContext(db, device, session, user, null, service, mockFs);
    }

    private SyncTestContext SetupWithSong(bool dryRun = false)
    {
        var ctx = Setup(dryRun);
        var artist = CreateArtist(ctx.Db, ctx.User.Id, "Artist");
        var album = CreateAlbum(ctx.Db, ctx.User.Id, "Album", artist);
        var song = CreateSong(ctx.Db, ctx.User.Id, "Song", album);
        return ctx with { Song = song };
    }

    private Device CreateDevice(MusicDbContext db, long ownerId, string name)
    {
        var device = new Device
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            NamingTemplate = "/music/{Artist}/{Album}/{Title}",
        };
        db.Add(device);
        db.SaveChanges();
        return device;
    }

    private DeviceSyncSession CreateSession(MusicDbContext db, Device device, bool dryRun = false)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = SyncSessionStatus.InProgress,
            IsDryRun = dryRun,
            Records = [],
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    private Artist CreateArtist(MusicDbContext db, long ownerId, string name)
    {
        var artist = new Artist
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        db.Add(artist);
        db.SaveChanges();
        return artist;
    }

    private Album CreateAlbum(MusicDbContext db, long ownerId, string name, Artist artist)
    {
        var album = new Album
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            ArtistId = artist.Id,
            Artist = artist,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        db.Add(album);
        db.SaveChanges();
        return album;
    }

    private Song CreateSong(MusicDbContext db, long ownerId, string title, Album album)
    {
        var song = new Song
        {
            Title = title,
            Label = title,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            AlbumId = album.Id,
            Album = album,
            Duration = TimeSpan.FromMinutes(3),
            Size = 3000000,
            RepositoryPath = $"/data/{title}.mp3",
            Checksum = "abc123",
            ChecksumAlgorithm = "XxHash128",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        db.Add(song);
        db.SaveChanges();
        return song;
    }

    private SongDevice CreateSongDevice(MusicDbContext db, long deviceId, long? songId, string devicePath, SongSyncAction? syncAction = null, DateTime? lastSyncedModifiedAt = null)
    {
        var songDevice = new SongDevice
        {
            SongId = songId,
            DeviceId = deviceId,
            DevicePath = devicePath,
            AddedAt = DateTime.UtcNow,
            SyncAction = syncAction,
            LastSyncedModifiedAt = lastSyncedModifiedAt,
        };
        db.SongDevices.Add(songDevice);
        db.SaveChanges();
        return songDevice;
    }

    private DeviceSyncSessionRecord AddRecord(MusicDbContext db, long sessionId, string filePath, SyncRecordAction action, JsonElement? data = null, long? songId = null, bool acknowledged = true)
    {
        var record = new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = filePath,
            Action = action,
            Data = data,
            SongId = songId,
            Acknowledged = acknowledged,
            ProcessedAt = DateTime.UtcNow,
        };
        db.DeviceSyncSessionRecords.Add(record);
        db.SaveChanges();
        return record;
    }

    private DeviceSyncSessionRecord AddSkippedRecord(MusicDbContext db, long sessionId, string filePath, long songId, DateTime modifiedAt)
    {
        var data = JsonSerializer.SerializeToElement(new { modifiedAt = modifiedAt.ToString("O") });
        return AddRecord(db, sessionId, filePath, SyncRecordAction.Skipped, data, songId);
    }

    private SongDevice GetSongDevice(MusicDbContext db, long songDeviceId) =>
        db.SongDevices.First(s => s.Id == songDeviceId);

    private SongDevice? FindSongDevice(MusicDbContext db, long songDeviceId) =>
        db.SongDevices.FirstOrDefault(s => s.Id == songDeviceId);

    private bool SongDeviceExists(MusicDbContext db, long songDeviceId) =>
        db.SongDevices.Any(s => s.Id == songDeviceId);

    private void AssertImportCalled(int count = 1) =>
        _musicService.Received(count).ImportRepositorySongs(
            Arg.Any<MusicDbContext>(), Arg.Any<MusicImportJob>(), Arg.Any<long>(),
            Arg.Any<IEnumerable<SongImportMetadata>>(), Arg.Any<IList<long>?>(),
            Arg.Any<DuplicateSongsHandlingStrategy>(), Arg.Any<CancellationToken>());

    private async Task AssertAddSongsToDeviceCalled(int count, long deviceId, long songId, string devicePath, DateTime modifiedAt) =>
        await _musicService.Received(count).AddSongsToDevice(Arg.Any<MusicDbContext>(), deviceId, songId, devicePath, modifiedAt, Arg.Any<CancellationToken>());

    private async Task AssertAddSongsToDeviceNotCalled()
    {
        await _musicService.DidNotReceive().AddSongsToDevice(Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _musicService.DidNotReceive().AddSongsToDevice(Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<Song>(), Arg.Any<CancellationToken>());
    }

    private static JsonElement CreateSyncData(long songId, DateTime modifiedAt, string? tempFilePath = null, string? checksum = null, string? algorithm = null, string? originalFilePath = null)
    {
        var dict = new Dictionary<string, object?>
        {
            ["songId"] = songId,
            ["modifiedAt"] = modifiedAt.ToString("O"),
        };
        if (tempFilePath is not null) dict["tempFilePath"] = tempFilePath;
        if (checksum is not null) dict["checksum"] = checksum;
        if (algorithm is not null) dict["algorithm"] = algorithm;
        if (originalFilePath is not null) dict["originalFilePath"] = originalFilePath;
        return JsonSerializer.SerializeToElement(dict);
    }

    private static JsonElement CreateLocalUpdateData(long songId, DateTime modifiedAt) =>
        JsonSerializer.SerializeToElement(new { songId, modifiedAt = modifiedAt.ToString("O") });

    private static JsonElement CreateSongIdData(long songId) =>
        JsonSerializer.SerializeToElement(new { songId });

    private static JsonElement CreateRenameData(string previousPath, string newPath) =>
        JsonSerializer.SerializeToElement(new { previousPath, newPath });

    private static JsonElement CreateConflictData(DateTime localModifiedAt, DateTime serverModifiedAt) =>
        JsonSerializer.SerializeToElement(new { localModifiedAt = localModifiedAt.ToString("O"), serverModifiedAt = serverModifiedAt.ToString("O") });

    private static JsonElement CreateErrorData(string errorMessage) =>
        JsonSerializer.SerializeToElement(new { errorMessage });

    private static JsonElement CreateUpdateTimestampData(long songId, DateTime newTimestamp) =>
        JsonSerializer.SerializeToElement(new { newTimestamp = newTimestamp.ToString("O"), songId });

    private async Task AssertDryRunSkipsMutation(Func<SyncTestContext, Task> arrange, SyncTestContext ctx, Action<SongDevice> assertUnchanged)
    {
        await arrange(ctx);
        var sdId = ctx.Db.SongDevices.OrderByDescending(s => s.Id).First().Id;
        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, true, cancellationToken: default);
        assertUnchanged(GetSongDevice(ctx.Db, sdId));
    }

    #region CreateRemote

    [Fact]
    public async Task CreateRemote_WithTempFile_ImportsSongAndAddsToDevice()
    {
        var ctx = SetupWithSong();
        var tempFilePath = "/data/.temp/sync-1/test.mp3";
        ctx.MockFs.AddFile(tempFilePath, new MockFileData("fake mp3"));
        var data = CreateSyncData(ctx.Song!.Id, DefaultModifiedAt, tempFilePath, checksum: "abc", algorithm: "XxHash128");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        AssertImportCalled(1);
        await AssertAddSongsToDeviceCalled(1, ctx.Device.Id, ctx.Song.Id, "/music/song.mp3", DefaultModifiedAt);
    }

    [Fact]
    public async Task CreateRemote_WithTempFileAndOriginalFilePath_PassesOriginalFilePathToImport()
    {
        var ctx = SetupWithSong();
        var tempFilePath = "/data/.temp/sync-1/abc123-track_a.mp3";
        var originalFilePath = "/data/.temp/sync-1/track_a.mp3";
        ctx.MockFs.AddFile(tempFilePath, new MockFileData("fake mp3"));
        var data = CreateSyncData(ctx.Song!.Id, DefaultModifiedAt, tempFilePath, checksum: "abc", algorithm: "XxHash128", originalFilePath: originalFilePath);
        AddRecord(ctx.Db, ctx.Session.Id, "track_a.mp3", SyncRecordAction.CreateRemote, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        await _musicService.Received(1).ImportRepositorySongs(
            Arg.Any<MusicDbContext>(),
            Arg.Any<MusicImportJob>(),
            Arg.Any<long>(),
            Arg.Is<IEnumerable<SongImportMetadata>>(m => m.Any(x => x.OriginalFilePath == originalFilePath)),
            Arg.Any<IList<long>?>(),
            Arg.Any<DuplicateSongsHandlingStrategy>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRemote_WithoutTempFile_SkipsImportAndAddsToDevice()
    {
        var ctx = SetupWithSong();
        var data = CreateSyncData(ctx.Song!.Id, DefaultModifiedAt, checksum: "abc", algorithm: "XxHash128");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        AssertImportCalled(0);
        await AssertAddSongsToDeviceCalled(1, ctx.Device.Id, ctx.Song.Id, "/music/song.mp3", DefaultModifiedAt);
    }

    [Fact]
    public async Task CreateRemote_DryRun_SkipsAllMutations()
    {
        var ctx = SetupWithSong(dryRun: true);
        var tempFilePath = "/data/.temp/sync-1/test.mp3";
        var data = CreateSyncData(ctx.Song!.Id, DefaultModifiedAt, tempFilePath, checksum: "abc", algorithm: "XxHash128");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, true, cancellationToken: default);

        AssertImportCalled(0);
        await AssertAddSongsToDeviceNotCalled();
    }

    #endregion

    #region UpdateRemote

    [Fact]
    public async Task UpdateRemote_WithTempFile_ImportsAndUpdatesTimestamp()
    {
        var ctx = SetupWithSong();
        var tempFilePath = "/data/.temp/sync-1/test.mp3";
        ctx.MockFs.AddFile(tempFilePath, new MockFileData("fake mp3"));
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var data = CreateSyncData(ctx.Song.Id, DefaultModifiedAt, tempFilePath, checksum: "abc", algorithm: "XxHash128");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateRemote, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        GetSongDevice(ctx.Db, sd.Id).LastSyncedModifiedAt.ShouldBe(DefaultModifiedAt);
    }

    [Fact]
    public async Task UpdateRemote_WithoutTempFile_UpdatesTimestampOnly()
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var data = CreateSyncData(ctx.Song.Id, DefaultModifiedAt, checksum: "abc", algorithm: "XxHash128");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateRemote, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        AssertImportCalled(0);
        GetSongDevice(ctx.Db, sd.Id).LastSyncedModifiedAt.ShouldBe(DefaultModifiedAt);
    }

    [Fact]
    public async Task UpdateRemote_DryRun_SkipsAllMutations()
    {
        var ctx = SetupWithSong(dryRun: true);

        await AssertDryRunSkipsMutation(c =>
        {
            CreateSongDevice(c.Db, c.Device.Id, c.Song!.Id, "/music/song.mp3");
            var data = CreateSyncData(c.Song!.Id, DefaultModifiedAt, checksum: "abc", algorithm: "XxHash128");
            AddRecord(c.Db, c.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateRemote, data, c.Song.Id);
            return Task.CompletedTask;
        }, ctx, sd => sd.LastSyncedModifiedAt.ShouldBeNull());
    }

    #endregion

    #region CreateLocal / UpdateLocal

    [Theory]
    [InlineData(SyncRecordAction.CreateLocal)]
    [InlineData(SyncRecordAction.UpdateLocal)]
    public async Task CreateLocalAndUpdateLocal_ClearsSyncActionAndSetsLastSyncedModifiedAt(SyncRecordAction action)
    {
        var ctx = SetupWithSong();
        var data = CreateLocalUpdateData(ctx.Song!.Id, DefaultModifiedAt);
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song.Id, "/music/song.mp3", syncAction: SongSyncAction.Download);
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", action, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        var updated = GetSongDevice(ctx.Db, sd.Id);
        updated.SyncAction.ShouldBeNull();
        updated.SyncActionReason.ShouldBeNull();
        updated.LastSyncedModifiedAt.ShouldBe(DefaultModifiedAt);
    }

    [Theory]
    [InlineData(SyncRecordAction.CreateLocal)]
    [InlineData(SyncRecordAction.UpdateLocal)]
    public async Task CreateLocalAndUpdateLocal_DryRun_SkipsMutation(SyncRecordAction action)
    {
        var ctx = SetupWithSong(dryRun: true);

        await AssertDryRunSkipsMutation(c =>
        {
            CreateSongDevice(c.Db, c.Device.Id, c.Song!.Id, "/music/song.mp3");
            AddRecord(c.Db, c.Session.Id, "/music/song.mp3", action, songId: c.Song!.Id);
            return Task.CompletedTask;
        }, ctx, sd => sd.SyncAction.ShouldBeNull());
    }

    #endregion

    #region Delete / Unlink

    [Theory]
    [InlineData(SyncRecordAction.Delete)]
    [InlineData(SyncRecordAction.Unlink)]
    public async Task DeleteAndUnlink_RemovesSongDevice(SyncRecordAction action)
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var data = CreateSongIdData(ctx.Song.Id);
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", action, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        SongDeviceExists(ctx.Db, sd.Id).ShouldBeFalse();
    }

    [Theory]
    [InlineData(SyncRecordAction.Delete)]
    [InlineData(SyncRecordAction.Unlink)]
    public async Task DeleteAndUnlink_DryRun_SkipsRemoval(SyncRecordAction action)
    {
        var ctx = SetupWithSong(dryRun: true);

        await AssertDryRunSkipsMutation(c =>
        {
            CreateSongDevice(c.Db, c.Device.Id, c.Song!.Id, "/music/song.mp3");
            var data = CreateSongIdData(c.Song!.Id);
            AddRecord(c.Db, c.Session.Id, "/music/song.mp3", action, data, c.Song!.Id);
            return Task.CompletedTask;
        }, ctx, _ => SongDeviceExists(ctx.Db, ctx.Db.SongDevices.OrderByDescending(s => s.Id).First().Id).ShouldBeTrue());
    }

    #endregion

    #region Link

    [Fact]
    public async Task Link_UsesIdOverloadToAddSongToDevice()
    {
        var ctx = SetupWithSong();
        var data = CreateSongIdData(ctx.Song!.Id);
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Link, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        await _musicService.Received(1).AddSongsToDevice(Arg.Any<MusicDbContext>(), ctx.Device.Id, ctx.Song.Id, "/music/song.mp3", Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Link_DryRun_SkipsMutation()
    {
        var ctx = SetupWithSong(dryRun: true);
        var data = CreateSongIdData(ctx.Song!.Id);
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Link, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, true, cancellationToken: default);

        await AssertAddSongsToDeviceNotCalled();
    }

    #endregion

    #region Rename

    [Fact]
    public async Task Rename_UpdatesDevicePathAndClearsSyncAction()
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/old.mp3", SongSyncAction.Upload);
        var data = CreateRenameData("/music/old.mp3", "/music/new.mp3");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/old.mp3", SyncRecordAction.Rename, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        var updated = GetSongDevice(ctx.Db, sd.Id);
        updated.DevicePath.ShouldBe("/music/new.mp3");
        updated.SyncAction.ShouldBeNull();
        updated.SyncActionReason.ShouldBeNull();
    }

    [Fact]
    public async Task Rename_WithNewPathInFilePath_UsesPreviousPathForLookup()
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/old.mp3", SongSyncAction.Upload);
        var data = CreateRenameData("/music/old.mp3", "/music/new.mp3");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/new.mp3", SyncRecordAction.Rename, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        var updated = GetSongDevice(ctx.Db, sd.Id);
        updated.DevicePath.ShouldBe("/music/new.mp3");
        updated.SyncAction.ShouldBeNull();
        updated.SyncActionReason.ShouldBeNull();
    }

    [Fact]
    public async Task Rename_DryRun_SkipsMutation()
    {
        var ctx = SetupWithSong(dryRun: true);

        await AssertDryRunSkipsMutation(c =>
        {
            CreateSongDevice(c.Db, c.Device.Id, c.Song!.Id, "/music/old.mp3", SongSyncAction.Upload);
            var data = CreateRenameData("/music/old.mp3", "/music/new.mp3");
            AddRecord(c.Db, c.Session.Id, "/music/old.mp3", SyncRecordAction.Rename, data, c.Song!.Id);
            return Task.CompletedTask;
        }, ctx, sd =>
        {
            sd.DevicePath.ShouldBe("/music/old.mp3");
            sd.SyncAction.ShouldBe(SongSyncAction.Upload);
        });
    }

    #endregion

    #region Skipped

    [Fact]
    public async Task Skipped_UpdatesLastSyncedModifiedAt()
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        GetSongDevice(ctx.Db, sd.Id).LastSyncedModifiedAt.ShouldBe(DefaultModifiedAt);
    }

    [Fact]
    public async Task Skipped_DryRun_SkipsMutation()
    {
        var ctx = SetupWithSong(dryRun: true);

        await AssertDryRunSkipsMutation(c =>
        {
            CreateSongDevice(c.Db, c.Device.Id, c.Song!.Id, "/music/song.mp3");
            AddSkippedRecord(c.Db, c.Session.Id, "/music/song.mp3", c.Song!.Id, DefaultModifiedAt);
            return Task.CompletedTask;
        }, ctx, sd => sd.LastSyncedModifiedAt.ShouldBeNull());
    }

    #endregion

    #region Conflict

    [Fact]
    public async Task Conflict_DoesNotMutateSongDevices()
    {
        var ctx = SetupWithSong();
        var lastSyncedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3", SongSyncAction.Download, lastSyncedAt);
        var localModified = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var serverModified = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var data = CreateConflictData(localModified, serverModified);
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Conflict, data, ctx.Song.Id);

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "down", cancellationToken: default);

        result.ShouldNotBeNull();
        result.ActionCounts.ShouldContainKey(SyncRecordAction.Conflict);
        var unchanged = GetSongDevice(ctx.Db, sd.Id);
        unchanged.SyncAction.ShouldBe(SongSyncAction.Download);
        unchanged.LastSyncedModifiedAt.ShouldBe(lastSyncedAt);
    }

    #endregion

    #region Error

    [Fact]
    public async Task Error_WithoutSongId_IsNoOp()
    {
        var ctx = Setup();
        var data = CreateErrorData("Something failed");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Error, data);

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task Error_WithSongId_DoesNotMutateSongDevices()
    {
        var ctx = SetupWithSong();
        var lastSyncedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3", SongSyncAction.Upload, lastSyncedAt);
        var data = CreateErrorData("Something failed");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Error, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "down", cancellationToken: default);

        var unchanged = GetSongDevice(ctx.Db, sd.Id);
        unchanged.SyncAction.ShouldBe(SongSyncAction.Upload);
        unchanged.LastSyncedModifiedAt.ShouldBe(lastSyncedAt);
    }

    #endregion

    #region UpdateTimestamp

    [Fact]
    public async Task UpdateTimestamp_UpdatesLastSyncedModifiedAt()
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var newTimestamp = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var data = CreateUpdateTimestampData(ctx.Song.Id, newTimestamp);
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateTimestamp, data, ctx.Song.Id);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "down", cancellationToken: default);

        GetSongDevice(ctx.Db, sd.Id).LastSyncedModifiedAt.ShouldBe(newTimestamp);
    }

    [Fact]
    public async Task UpdateTimestamp_DryRun_SkipsMutation()
    {
        var ctx = SetupWithSong(dryRun: true);

        await AssertDryRunSkipsMutation(c =>
        {
            CreateSongDevice(c.Db, c.Device.Id, c.Song!.Id, "/music/song.mp3");
            var newTimestamp = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
            var data = CreateUpdateTimestampData(c.Song!.Id, newTimestamp);
            AddRecord(c.Db, c.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateTimestamp, data, c.Song!.Id);
            return Task.CompletedTask;
        }, ctx, sd => sd.LastSyncedModifiedAt.ShouldBeNull());
    }

    #endregion

    #region Records Processed By Id Ascending

    [Fact]
    public async Task Records_AreProcessedByIdAscending()
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var newerModifiedAt = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, newerModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        GetSongDevice(ctx.Db, sd.Id).LastSyncedModifiedAt.ShouldBe(newerModifiedAt);
    }

    #endregion

    #region Idempotency

    [Fact]
    public async Task CommitAsync_AlreadyCommittedSession_ReturnsExistingResult()
    {
        var ctx = SetupWithSong();
        ctx.Session.Status = SyncSessionStatus.Committed;
        ctx.Session.CompletedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        ctx.Db.SaveChanges();
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song!.Id, DefaultModifiedAt);

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.CommittedAt.ShouldBe(ctx.Session.CompletedAt.Value);
    }

    #endregion

    #region Missing Staged File Handling

    [Fact]
    public async Task CreateRemote_MissingStagedFile_RecordsErrorAndContinues()
    {
        var ctx = SetupWithSong();
        var tempFilePath = "/data/.temp/sync-1/missing.mp3";
        var data = CreateSyncData(ctx.Song!.Id, DefaultModifiedAt, tempFilePath, checksum: "abc", algorithm: "XxHash128");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, data, ctx.Song.Id);

        var otherModifiedAt = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var otherSong = CreateSong(ctx.Db, ctx.User.Id, "OtherSong", ctx.Song.Album);
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/other.mp3", otherSong.Id, otherModifiedAt);
        CreateSongDevice(ctx.Db, ctx.Device.Id, otherSong.Id, "/music/other.mp3");

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.ActionCounts.ShouldContainKey(SyncRecordAction.Error);
        result.ActionCounts[SyncRecordAction.Error].ShouldBeGreaterThanOrEqualTo(1);
        var otherSd = ctx.Db.SongDevices.FirstOrDefault(sd => sd.DevicePath == "/music/other.mp3");
        otherSd.ShouldNotBeNull();
        otherSd.LastSyncedModifiedAt.ShouldBe(otherModifiedAt);
    }

    [Fact]
    public async Task UpdateRemote_MissingStagedFile_RecordsErrorAndContinues()
    {
        var ctx = SetupWithSong();
        var tempFilePath = "/data/.temp/sync-1/missing.mp3";
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var data = CreateSyncData(ctx.Song.Id, DefaultModifiedAt, tempFilePath, checksum: "abc", algorithm: "XxHash128");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateRemote, data, ctx.Song.Id);

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.ActionCounts.ShouldContainKey(SyncRecordAction.Error);
        FindSongDevice(ctx.Db, sd.Id).ShouldNotBeNull();
    }

    #endregion

    #region Orphan Detection

    [Fact]
    public async Task OrphanDetection_BothDirection_DetectsOrphansWithNullSyncAction()
    {
        var ctx = SetupWithSong();
        CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var orphan = CreateSongDevice(ctx.Db, ctx.Device.Id, null, "/music/orphan.mp3");
        var orphanId = orphan.Id;
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "both", cancellationToken: default);

        SongDeviceExists(ctx.Db, orphanId).ShouldBeFalse();
        var unlinkRecords = ctx.Db.DeviceSyncSessionRecords.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.Count.ShouldBe(1);
        unlinkRecords[0].FilePath.ShouldBe("/music/orphan.mp3");
    }

    [Fact]
    public async Task OrphanDetection_BothDirection_IgnoresOrphansWithNonNullSyncAction()
    {
        var ctx = SetupWithSong();
        CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var notOrphan = CreateSongDevice(ctx.Db, ctx.Device.Id, null, "/music/not-orphan.mp3", SongSyncAction.Download);
        var notOrphanId = notOrphan.Id;
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "both", cancellationToken: default);

        SongDeviceExists(ctx.Db, notOrphanId).ShouldBeTrue();
    }

    [Fact]
    public async Task OrphanDetection_UpDirection_DetectsAllOrphans()
    {
        var ctx = SetupWithSong();
        CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        var orphan1 = CreateSongDevice(ctx.Db, ctx.Device.Id, null, "/music/orphan1.mp3");
        var orphan2 = CreateSongDevice(ctx.Db, ctx.Device.Id, null, "/music/orphan2.mp3", SongSyncAction.Download);
        var orphan1Id = orphan1.Id;
        var orphan2Id = orphan2.Id;
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "up", cancellationToken: default);

        SongDeviceExists(ctx.Db, orphan1Id).ShouldBeFalse();
        SongDeviceExists(ctx.Db, orphan2Id).ShouldBeFalse();
    }

    [Fact]
    public async Task OrphanDetection_UpDirection_ClearsSyncActionsOnValidPaths()
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3", SongSyncAction.Upload);
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "up", cancellationToken: default);

        var updated = GetSongDevice(ctx.Db, sd.Id);
        updated.SyncAction.ShouldBeNull();
        updated.SyncActionReason.ShouldBeNull();
    }

    [Fact]
    public async Task OrphanDetection_DownDirection_SkipsDetection()
    {
        var ctx = SetupWithSong();
        var potentialOrphan = CreateSongDevice(ctx.Db, ctx.Device.Id, null, "/music/orphan.mp3");
        var orphanId = potentialOrphan.Id;
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);
        CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "down", cancellationToken: default);

        SongDeviceExists(ctx.Db, orphanId).ShouldBeTrue();
        var unlinkRecords = ctx.Db.DeviceSyncSessionRecords.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task OrphanDetection_DryRun_CreatesUnlinkRecordsWithoutRemovingSongDevices()
    {
        var ctx = SetupWithSong(dryRun: true);
        var orphan = CreateSongDevice(ctx.Db, ctx.Device.Id, null, "/music/orphan.mp3");
        var orphanId = orphan.Id;
        CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, true, direction: "both", cancellationToken: default);

        SongDeviceExists(ctx.Db, orphanId).ShouldBeTrue();
        var unlinkRecords = ctx.Db.DeviceSyncSessionRecords.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.Count.ShouldBe(1);
    }

    #endregion

    #region Delete / Unlink FindSongDeviceByIds Fallback

    [Theory]
    [InlineData(SyncRecordAction.Delete)]
    [InlineData(SyncRecordAction.Unlink)]
    public async Task DeleteAndUnlink_WhenNoSongId_FallsBackToDevicePathLookup(SyncRecordAction action)
    {
        var ctx = SetupWithSong();
        var sd = CreateSongDevice(ctx.Db, ctx.Device.Id, ctx.Song!.Id, "/music/song.mp3");
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song.mp3", action);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "down", cancellationToken: default);

        SongDeviceExists(ctx.Db, sd.Id).ShouldBeFalse();
    }

    #endregion

    #region Result ActionCounts

    [Fact]
    public async Task CommitAsync_ReturnsCorrectActionCounts()
    {
        var ctx = SetupWithSong();
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song1.mp3", ctx.Song!.Id, DefaultModifiedAt);
        AddSkippedRecord(ctx.Db, ctx.Session.Id, "/music/song2.mp3", ctx.Song.Id, DefaultModifiedAt);
        AddRecord(ctx.Db, ctx.Session.Id, "/music/song3.mp3", SyncRecordAction.CreateLocal, songId: ctx.Song.Id);

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.ActionCounts[SyncRecordAction.Skipped].ShouldBe(2);
        result.ActionCounts[SyncRecordAction.CreateLocal].ShouldBe(1);
    }

    #endregion
}