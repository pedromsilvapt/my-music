using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncSessionRecordsQueryServiceSpecs
{
    private static SyncSessionRecordsQueryService CreateService(Scenario scenario) =>
        new(scenario.DbContext, new SyncSessionLookupService());

    [Fact]
    public async Task QueryAsync_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");

        // Act
        var result = await CreateService(scenario).QueryAsync(
            9999, device.Id, scenario.AdminUser.Id, null, null, null, null, null, null, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task QueryAsync_OtherUsersSession_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed);

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, otherDevice.Id, scenario.AdminUser.Id, null, null, null, null, null, null, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task QueryAsync_ReturnsAllRecordsWhenNoLimit()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, device.Id, scenario.AdminUser.Id, null, null, null, null, null, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(2);
        result.HasMore.ShouldBeFalse();
        result.NextCursor.ShouldBeNull();
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_Limit_SetsHasMoreAndNextCursor()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        for (var i = 0; i < 5; i++)
        {
            scenario.AddRecord(session.Id, $"/{i}.mp3", SyncRecordAction.CreateRemote);
        }

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, device.Id, scenario.AdminUser.Id, null, limit: 2, null, null, null, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(2);
        result.HasMore.ShouldBeTrue();
        result.NextCursor.ShouldBe("2");
        result.TotalCount.ShouldBe(5);
    }

    [Fact]
    public async Task QueryAsync_Offset_AdvancesCursor()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        for (var i = 0; i < 5; i++)
        {
            scenario.AddRecord(session.Id, $"/{i}.mp3", SyncRecordAction.CreateRemote);
        }

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, device.Id, scenario.AdminUser.Id, null, limit: 2, offset: 2, null, null, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(2);
        result.HasMore.ShouldBeTrue();
        result.NextCursor.ShouldBe("4");
    }

    [Fact]
    public async Task QueryAsync_ActionsFilter_RestrictsToActions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/c.mp3", SyncRecordAction.Error);

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, device.Id, scenario.AdminUser.Id, "Skipped,Error", null, null, null, null, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(2);
        result.Records.All(r => r.Action == SyncRecordAction.Skipped || r.Action == SyncRecordAction.Error).ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_DefaultSort_OrdersByIdAscending()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var r1 = scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        var r2 = scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, device.Id, scenario.AdminUser.Id, null, null, null, null, null, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Select(r => r.Id).ShouldBe([r1.Id, r2.Id]);
    }

    [Fact]
    public async Task QueryAsync_SortActionDate_OrdersByActionThenProcessedAtThenId()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var r1 = scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);
        var r2 = scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, device.Id, scenario.AdminUser.Id, null, null, null, sort: "action_date", null, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Select(r => r.Action).ShouldBe([SyncRecordAction.CreateRemote, SyncRecordAction.Skipped]);
    }

    [Fact]
    public async Task QueryAsync_OnlyReturnsRecordsForSpecifiedSession()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session1 = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var session2 = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session1.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session2.Id, "/b.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session1.Id, device.Id, scenario.AdminUser.Id, null, null, null, null, null, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].FilePath.ShouldBe("/a.mp3");
    }

    [Fact]
    public async Task QueryAsync_IncludeSongInfo_LoadsSongAndArtists()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Title");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote, songId: song.Id);

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, device.Id, scenario.AdminUser.Id, null, null, null, null, includeSongInfo: true, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Song.ShouldNotBeNull();
        record.Song.Artists.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task QueryAsync_DslFilter_SongTitleContains()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var song1 = scenario.CreateSong("Galaxy Song");
        var song2 = scenario.CreateSong("Other Tune");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote, songId: song1.Id);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.CreateRemote, songId: song2.Id);

        // Act
        var result = await CreateService(scenario).QueryAsync(
            session.Id, device.Id, scenario.AdminUser.Id, null, null, null, null, includeSongInfo: true,
            filter: @"song.title contains ""Galaxy""", CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(1);
        result.Records.Single().Song!.Title.ShouldBe("Galaxy Song");
    }
}