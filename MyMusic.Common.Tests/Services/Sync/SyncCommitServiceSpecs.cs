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
        Scenario Scenario,
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
        var device = scenario.CreateDevice("Phone", namingTemplate: "/music/{Artist}/{Album}/{Title}");
        var session = scenario.CreateSession(device, isDryRun: dryRun);
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);
        return new SyncTestContext(scenario, db, device, session, user, null, service, mockFs);
    }

    private SyncTestContext SetupWithSong(bool dryRun = false)
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var artist = scenario.CreateArtist("Artist");
        var album = scenario.CreateAlbum("Album", artist);
        var song = scenario.CreateSong("Song", album: album);
        var device = scenario.CreateDevice("Phone", namingTemplate: "/music/{Artist}/{Album}/{Title}");
        var session = scenario.CreateSession(device, isDryRun: dryRun);
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);
        return new SyncTestContext(scenario, db, device, session, user, song, service, mockFs);
    }



    private DeviceSyncSessionRecord AddSkippedRecord(Scenario scenario, long sessionId, string filePath, long songId, DateTime modifiedAt)
    {
        var data = JsonSerializer.SerializeToElement(new { modifiedAt = modifiedAt.ToString("O") });
        return scenario.AddRecord(sessionId, filePath, SyncRecordAction.Skipped, data: data, songId: songId, acknowledged: true);
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
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, data: data, songId: ctx.Song.Id, acknowledged: true);

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
        ctx.Scenario.AddRecord(ctx.Session.Id, "track_a.mp3", SyncRecordAction.CreateRemote, data: data, songId: ctx.Song.Id, acknowledged: true);

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
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, data: data, songId: ctx.Song.Id, acknowledged: true);

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
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, data: data, songId: ctx.Song.Id, acknowledged: true);

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
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var data = CreateSyncData(ctx.Song.Id, DefaultModifiedAt, tempFilePath, checksum: "abc", algorithm: "XxHash128");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateRemote, data: data, songId: ctx.Song.Id, acknowledged: true);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        GetSongDevice(ctx.Db, sd.Id).LastSyncedModifiedAt.ShouldBe(DefaultModifiedAt);
    }

    [Fact]
    public async Task UpdateRemote_WithoutTempFile_UpdatesTimestampOnly()
    {
        var ctx = SetupWithSong();
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var data = CreateSyncData(ctx.Song.Id, DefaultModifiedAt, checksum: "abc", algorithm: "XxHash128");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateRemote, data: data, songId: ctx.Song.Id, acknowledged: true);

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
            ctx.Scenario.CreateSongDevice(c.Device, c.Song, "/music/song.mp3");
            var data = CreateSyncData(c.Song!.Id, DefaultModifiedAt, checksum: "abc", algorithm: "XxHash128");
            c.Scenario.AddRecord(c.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateRemote, data: data, songId: c.Song.Id, acknowledged: true);
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
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3", syncAction: SongSyncAction.Download);
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", action, data: data, songId: ctx.Song.Id, acknowledged: true);

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
            ctx.Scenario.CreateSongDevice(c.Device, c.Song, "/music/song.mp3");
            c.Scenario.AddRecord(c.Session.Id, "/music/song.mp3", action, songId: c.Song!.Id, acknowledged: true);
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
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var data = CreateSongIdData(ctx.Song.Id);
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", action, data: data, songId: ctx.Song.Id, acknowledged: true);

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
            ctx.Scenario.CreateSongDevice(c.Device, c.Song, "/music/song.mp3");
            var data = CreateSongIdData(c.Song!.Id);
            c.Scenario.AddRecord(c.Session.Id, "/music/song.mp3", action, data: data, songId: c.Song!.Id, acknowledged: true);
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
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Link, data: data, songId: ctx.Song.Id, acknowledged: true);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        await _musicService.Received(1).AddSongsToDevice(Arg.Any<MusicDbContext>(), ctx.Device.Id, ctx.Song.Id, "/music/song.mp3", Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Link_DryRun_SkipsMutation()
    {
        var ctx = SetupWithSong(dryRun: true);
        var data = CreateSongIdData(ctx.Song!.Id);
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Link, data: data, songId: ctx.Song.Id, acknowledged: true);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, true, cancellationToken: default);

        await AssertAddSongsToDeviceNotCalled();
    }

    #endregion

    #region Rename

    [Fact]
    public async Task Rename_UpdatesDevicePathAndClearsSyncAction()
    {
        var ctx = SetupWithSong();
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/old.mp3", syncAction: SongSyncAction.Upload);
        var data = CreateRenameData("/music/old.mp3", "/music/new.mp3");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/old.mp3", SyncRecordAction.Rename, data: data, songId: ctx.Song.Id, acknowledged: true);

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
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/old.mp3", syncAction: SongSyncAction.Upload);
        var data = CreateRenameData("/music/old.mp3", "/music/new.mp3");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/new.mp3", SyncRecordAction.Rename, data: data, songId: ctx.Song.Id, acknowledged: true);

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
            ctx.Scenario.CreateSongDevice(c.Device, c.Song, "/music/old.mp3", syncAction: SongSyncAction.Upload);
            var data = CreateRenameData("/music/old.mp3", "/music/new.mp3");
            c.Scenario.AddRecord(c.Session.Id, "/music/old.mp3", SyncRecordAction.Rename, data: data, songId: c.Song!.Id, acknowledged: true);
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
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        GetSongDevice(ctx.Db, sd.Id).LastSyncedModifiedAt.ShouldBe(DefaultModifiedAt);
    }

    [Fact]
    public async Task Skipped_DryRun_SkipsMutation()
    {
        var ctx = SetupWithSong(dryRun: true);

        await AssertDryRunSkipsMutation(c =>
        {
            ctx.Scenario.CreateSongDevice(c.Device, c.Song, "/music/song.mp3");
            AddSkippedRecord(c.Scenario, c.Session.Id, "/music/song.mp3", c.Song!.Id, DefaultModifiedAt);
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
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3", lastSyncedModifiedAt: lastSyncedAt, syncAction: SongSyncAction.Download);
        var localModified = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var serverModified = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var data = CreateConflictData(localModified, serverModified);
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Conflict, data: data, songId: ctx.Song.Id, acknowledged: true);

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
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Error, data: data, acknowledged: true);

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task Error_WithSongId_DoesNotMutateSongDevices()
    {
        var ctx = SetupWithSong();
        var lastSyncedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3", lastSyncedModifiedAt: lastSyncedAt, syncAction: SongSyncAction.Upload);
        var data = CreateErrorData("Something failed");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.Error, data: data, songId: ctx.Song.Id, acknowledged: true);

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
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var newTimestamp = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var data = CreateUpdateTimestampData(ctx.Song.Id, newTimestamp);
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateTimestamp, data: data, songId: ctx.Song.Id, acknowledged: true);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "down", cancellationToken: default);

        GetSongDevice(ctx.Db, sd.Id).LastSyncedModifiedAt.ShouldBe(newTimestamp);
    }

    [Fact]
    public async Task UpdateTimestamp_DryRun_SkipsMutation()
    {
        var ctx = SetupWithSong(dryRun: true);

        await AssertDryRunSkipsMutation(c =>
        {
            ctx.Scenario.CreateSongDevice(c.Device, c.Song, "/music/song.mp3");
            var newTimestamp = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
            var data = CreateUpdateTimestampData(c.Song!.Id, newTimestamp);
            c.Scenario.AddRecord(c.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateTimestamp, data: data, songId: c.Song!.Id, acknowledged: true);
            return Task.CompletedTask;
        }, ctx, sd => sd.LastSyncedModifiedAt.ShouldBeNull());
    }

    #endregion

    #region Records Processed By Id Ascending

    [Fact]
    public async Task Records_AreProcessedByIdAscending()
    {
        var ctx = SetupWithSong();
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var newerModifiedAt = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, newerModifiedAt);

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
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song!.Id, DefaultModifiedAt);

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
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.CreateRemote, data: data, songId: ctx.Song.Id, acknowledged: true);

        var otherModifiedAt = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var otherSong = ctx.Scenario.CreateSong("OtherSong", album: ctx.Song.Album);
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/other.mp3", otherSong.Id, otherModifiedAt);
        ctx.Scenario.CreateSongDevice(ctx.Device, otherSong, "/music/other.mp3");

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.ActionCounts.ShouldContainKey(SyncRecordAction.Error);
        result.ActionCounts[SyncRecordAction.Error].ShouldBeGreaterThanOrEqualTo(1);
        var errorRecord = ctx.Db.DeviceSyncSessionRecords.First(r => r.Action == SyncRecordAction.Error);
        errorRecord.Reason.ShouldStartWith("Staged file not found");
        var otherSd = ctx.Db.SongDevices.FirstOrDefault(sd => sd.DevicePath == "/music/other.mp3");
        otherSd.ShouldNotBeNull();
        otherSd.LastSyncedModifiedAt.ShouldBe(otherModifiedAt);
    }

    [Fact]
    public async Task UpdateRemote_MissingStagedFile_RecordsErrorAndContinues()
    {
        var ctx = SetupWithSong();
        var tempFilePath = "/data/.temp/sync-1/missing.mp3";
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var data = CreateSyncData(ctx.Song.Id, DefaultModifiedAt, tempFilePath, checksum: "abc", algorithm: "XxHash128");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", SyncRecordAction.UpdateRemote, data: data, songId: ctx.Song.Id, acknowledged: true);

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.ActionCounts.ShouldContainKey(SyncRecordAction.Error);
        FindSongDevice(ctx.Db, sd.Id).ShouldNotBeNull();
        var errorRecord = ctx.Db.DeviceSyncSessionRecords.First(r => r.Action == SyncRecordAction.Error);
        errorRecord.Reason.ShouldStartWith("Staged file not found");
    }

    #endregion

    #region Orphan Detection

    [Fact]
    public async Task OrphanDetection_BothDirection_DetectsOrphansWithNullSyncAction()
    {
        var ctx = SetupWithSong();
        ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var orphan = ctx.Scenario.CreateSongDevice(ctx.Device, null, "/music/orphan.mp3");
        var orphanId = orphan.Id;
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "both", cancellationToken: default);

        SongDeviceExists(ctx.Db, orphanId).ShouldBeFalse();
        var unlinkRecords = ctx.Db.DeviceSyncSessionRecords.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.Count.ShouldBe(1);
        unlinkRecords[0].FilePath.ShouldBe("/music/orphan.mp3");
        unlinkRecords[0].Reason.ShouldBe("Orphaned: path not present in sync session");
    }

    [Fact]
    public async Task OrphanDetection_BothDirection_IgnoresOrphansWithNonNullSyncAction()
    {
        var ctx = SetupWithSong();
        ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var notOrphan = ctx.Scenario.CreateSongDevice(ctx.Device, null, "/music/not-orphan.mp3", syncAction: SongSyncAction.Download);
        var notOrphanId = notOrphan.Id;
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "both", cancellationToken: default);

        SongDeviceExists(ctx.Db, notOrphanId).ShouldBeTrue();
    }

    [Fact]
    public async Task OrphanDetection_UpDirection_DetectsAllOrphans()
    {
        var ctx = SetupWithSong();
        ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        var orphan1 = ctx.Scenario.CreateSongDevice(ctx.Device, null, "/music/orphan1.mp3");
        var orphan2 = ctx.Scenario.CreateSongDevice(ctx.Device, null, "/music/orphan2.mp3", syncAction: SongSyncAction.Download);
        var orphan1Id = orphan1.Id;
        var orphan2Id = orphan2.Id;
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "up", cancellationToken: default);

        SongDeviceExists(ctx.Db, orphan1Id).ShouldBeFalse();
        SongDeviceExists(ctx.Db, orphan2Id).ShouldBeFalse();
    }

    [Fact]
    public async Task OrphanDetection_UpDirection_ClearsSyncActionsOnValidPaths()
    {
        var ctx = SetupWithSong();
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3", syncAction: SongSyncAction.Upload);
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "up", cancellationToken: default);

        var updated = GetSongDevice(ctx.Db, sd.Id);
        updated.SyncAction.ShouldBeNull();
        updated.SyncActionReason.ShouldBeNull();
    }

    [Fact]
    public async Task OrphanDetection_DownDirection_SkipsDetection()
    {
        var ctx = SetupWithSong();
        var potentialOrphan = ctx.Scenario.CreateSongDevice(ctx.Device, null, "/music/orphan.mp3");
        var orphanId = potentialOrphan.Id;
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);
        ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "down", cancellationToken: default);

        SongDeviceExists(ctx.Db, orphanId).ShouldBeTrue();
        var unlinkRecords = ctx.Db.DeviceSyncSessionRecords.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task OrphanDetection_DryRun_CreatesUnlinkRecordsWithoutRemovingSongDevices()
    {
        var ctx = SetupWithSong(dryRun: true);
        var orphan = ctx.Scenario.CreateSongDevice(ctx.Device, null, "/music/orphan.mp3");
        var orphanId = orphan.Id;
        ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song.mp3", ctx.Song.Id, DefaultModifiedAt);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, true, direction: "both", cancellationToken: default);

        SongDeviceExists(ctx.Db, orphanId).ShouldBeTrue();
        var unlinkRecords = ctx.Db.DeviceSyncSessionRecords.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OrphanDetection_BothDirection_RenameRecord_ProtectsPreviousPathFromOrphaning()
    {
        var ctx = SetupWithSong();
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/old.mp3");
        var renameData = CreateRenameData("/music/old.mp3", "/music/new.mp3");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/new.mp3", SyncRecordAction.Rename, data: renameData, songId: ctx.Song.Id, acknowledged: true);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "both", cancellationToken: default);

        // The SongDevice at PreviousPath should be preserved (not orphaned) and its path updated to NewPath
        var updated = GetSongDevice(ctx.Db, sd.Id);
        updated.DevicePath.ShouldBe("/music/new.mp3");
        var unlinkRecords = ctx.Db.DeviceSyncSessionRecords.Where(r => r.Action == SyncRecordAction.Unlink).ToList();
        unlinkRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task OrphanDetection_BothDirection_StandaloneRename_DoesNotOrphan()
    {
        // Simulates a future "manual rename" scenario: a Rename record with no paired
        // UpdateLocal/CreateLocal record. The SongDevice at PreviousPath must still be protected
        // from orphan detection (and its path moved to NewPath by ProcessRenameAsync).
        var ctx = SetupWithSong();
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/old.mp3");
        var renameData = CreateRenameData("/music/old.mp3", "/music/new.mp3");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/new.mp3", SyncRecordAction.Rename, data: renameData, songId: ctx.Song.Id, acknowledged: true);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "both", cancellationToken: default);

        SongDeviceExists(ctx.Db, sd.Id).ShouldBeTrue();
        GetSongDevice(ctx.Db, sd.Id).DevicePath.ShouldBe("/music/new.mp3");
        ctx.Db.DeviceSyncSessionRecords.ShouldNotContain(r => r.Action == SyncRecordAction.Unlink);
    }

    [Fact]
    public async Task OrphanDetection_UpDirection_RenameRecord_ClearsSyncActionOnPreviousPath()
    {
        var ctx = SetupWithSong();
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/old.mp3", syncAction: SongSyncAction.Download);
        var renameData = CreateRenameData("/music/old.mp3", "/music/new.mp3");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/new.mp3", SyncRecordAction.Rename, data: renameData, songId: ctx.Song.Id, acknowledged: true);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "up", cancellationToken: default);

        // ProcessRenameAsync updates DevicePath and clears SyncAction; the songsToClear block
        // (which uses the same validFilePaths set) is redundant but must not conflict.
        var updated = GetSongDevice(ctx.Db, sd.Id);
        updated.DevicePath.ShouldBe("/music/new.mp3");
        updated.SyncAction.ShouldBeNull();
        updated.SyncActionReason.ShouldBeNull();
    }

    #endregion

    #region Delete / Unlink FindSongDeviceByIds Fallback

    [Theory]
    [InlineData(SyncRecordAction.Delete)]
    [InlineData(SyncRecordAction.Unlink)]
    public async Task DeleteAndUnlink_WhenNoSongId_FallsBackToDevicePathLookup(SyncRecordAction action)
    {
        var ctx = SetupWithSong();
        var sd = ctx.Scenario.CreateSongDevice(ctx.Device, ctx.Song, "/music/song.mp3");
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song.mp3", action, acknowledged: true);

        await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, direction: "down", cancellationToken: default);

        SongDeviceExists(ctx.Db, sd.Id).ShouldBeFalse();
    }

    #endregion

    #region Result ActionCounts

    [Fact]
    public async Task CommitAsync_ReturnsCorrectActionCounts()
    {
        var ctx = SetupWithSong();
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song1.mp3", ctx.Song!.Id, DefaultModifiedAt);
        AddSkippedRecord(ctx.Scenario, ctx.Session.Id, "/music/song2.mp3", ctx.Song.Id, DefaultModifiedAt);
        ctx.Scenario.AddRecord(ctx.Session.Id, "/music/song3.mp3", SyncRecordAction.CreateLocal, songId: ctx.Song.Id, acknowledged: true);

        var result = await ctx.Service.CommitAsync(ctx.Db, ctx.Session.Id, ctx.Device.Id, false, cancellationToken: default);

        result.ActionCounts[SyncRecordAction.Skipped].ShouldBe(2);
        result.ActionCounts[SyncRecordAction.CreateLocal].ShouldBe(1);
    }

    #endregion
}