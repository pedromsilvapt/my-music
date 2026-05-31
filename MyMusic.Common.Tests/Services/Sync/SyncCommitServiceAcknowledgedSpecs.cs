using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Models;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncCommitServiceAcknowledgedSpecs
{
    private readonly IMusicService _musicService = Substitute.For<IMusicService>();
    private readonly ILogger<SyncCommitService> _logger = Substitute.For<ILogger<SyncCommitService>>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();

    public SyncCommitServiceAcknowledgedSpecs()
    {
        var importJobLogger = Substitute.For<ILogger<MusicImportJob>>();
        _loggerFactory.CreateLogger(typeof(MusicImportJob)).Returns(importJobLogger);
    }

    [Fact]
    public async Task CommitAsync_Fails_IfUnacknowledgedCreateLocalRecordsExist()
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device);
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);

        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal);

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CommitAsync(db, session.Id, device.Id, false, cancellationToken: default));

        ex.Message.ShouldContain("unacknowledged client-action records");
    }

    [Fact]
    public async Task CommitAsync_Fails_IfUnacknowledgedUnlinkRecordsExist()
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device);
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);

        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.Unlink);

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CommitAsync(db, session.Id, device.Id, false, cancellationToken: default));

        ex.Message.ShouldContain("unacknowledged client-action records");
    }

    [Fact]
    public async Task CommitAsync_Succeeds_WhenAllClientRecordsAcknowledged()
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device);
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);

        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.CreateLocal, acknowledged: true);

        var result = await service.CommitAsync(db, session.Id, device.Id, false, cancellationToken: default);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CommitAsync_Succeeds_WithUnacknowledgedServerActionRecords()
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device);
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);

        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/music/song2.mp3", SyncRecordAction.Conflict);

        var result = await service.CommitAsync(db, session.Id, device.Id, false, cancellationToken: default);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CommitAsync_AutoAcknowledgesServerActionRecords()
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device);
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);

        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.Skipped);

        await service.CommitAsync(db, session.Id, device.Id, false, cancellationToken: default);

        var record = await db.DeviceSyncSessionRecords.FirstAsync(r => r.SessionId == session.Id);
        record.Acknowledged.ShouldBeTrue();
    }

    [Fact]
    public async Task CommitAsync_DryRun_SucceedsWithUnacknowledgedClientActionRecords()
    {
        var scenario = new Scenario();
        var db = scenario.DbContext;
        var user = scenario.AdminUser;
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device);
        var service = new SyncCommitService(scenario.FileSystem, _musicService, _loggerFactory, _logger);

        scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.UpdateLocal);
        scenario.AddRecord(session.Id, "/music/song2.mp3", SyncRecordAction.CreateLocal);

        var result = await service.CommitAsync(db, session.Id, device.Id, true, cancellationToken: default);

        result.ShouldNotBeNull();
    }

}