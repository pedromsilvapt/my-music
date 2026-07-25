using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Filters;
using MyMusic.Server.DTO.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class SyncSessionsControllerSessionRecordsSpecs
{
    private SyncSessionsController CreateController(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        return new SyncSessionsController(
            Substitute.For<ILogger<SyncSessionsController>>(),
            currentUser,
            SyncSessionsControllerHelpers.CreateSyncSessionListService(scenario),
            SyncSessionsControllerHelpers.CreateSyncSessionRecordsQueryService(scenario),
            SyncSessionsControllerHelpers.CreateSyncSessionFilterValuesService(scenario));
    }

    #region GetSessionRecords

    [Fact]
    public async Task GetSessionRecords_ReturnsRecordsForSession()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await controller.GetSessionRecords(device.Id, session.Id);

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value.Records.Count.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(2);
        result.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetSessionRecords_SessionNotFound_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");

        // Act & Assert
        var result = await controller.GetSessionRecords(device.Id, 9999);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSessionRecords_OtherUsersDevice_ReturnsNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var otherSession = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed);
        scenario.AddRecord(otherSession.Id, "/x.mp3", SyncRecordAction.CreateRemote);

        // Act & Assert
        var result = await controller.GetSessionRecords(otherDevice.Id, otherSession.Id);
        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSessionRecords_WithLimit_SetsHasMoreAndNextCursor()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        for (var i = 0; i < 5; i++)
        {
            scenario.AddRecord(session.Id, $"/{i}.mp3", SyncRecordAction.CreateRemote);
        }

        // Act
        var result = await controller.GetSessionRecords(device.Id, session.Id, limit: 2);

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value.Records.Count.ShouldBe(2);
        result.Value.HasMore.ShouldBeTrue();
        result.Value.NextCursor.ShouldBe("2");
    }

    [Fact]
    public async Task GetSessionRecords_IncludeSongInfo_MapsSongInfoToDto()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Title");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote, songId: song.Id);

        // Act
        var result = await controller.GetSessionRecords(device.Id, session.Id, includeSongInfo: true);

        // Assert
        result.Value.ShouldNotBeNull();
        var dto = result.Value.Records.Single();
        dto.SongInfo.ShouldNotBeNull();
        dto.SongInfo!.Title.ShouldBe("Title");
    }

    [Fact]
    public async Task GetSessionRecords_ActionsFilter_RestrictsRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await controller.GetSessionRecords(device.Id, session.Id, actions: "Skipped");

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value.Records.Count.ShouldBe(1);
        result.Value.Records.Single().Action.ShouldBe(SyncRecordAction.Skipped);
    }

    #endregion

    #region GetSessionRecordsFilterMetadata

    [Fact]
    public void GetSessionRecordsFilterMetadata_ReturnsStaticFields()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);

        // Act
        var result = controller.GetSessionRecordsFilterMetadata(device.Id, session.Id);

        // Assert
        var names = result.Fields.Select(f => f.Name).ToList();
        names.ShouldContain("filePath");
        names.ShouldContain("action");
        names.ShouldContain("song");
        names.ShouldContain("song.title");
        names.ShouldContain("song.artist.name");
        names.ShouldContain("song.album.name");
        result.Operators.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetSessionRecordsFilterMetadata_ActionField_ExposesAllEnumValues()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);

        // Act
        var result = controller.GetSessionRecordsFilterMetadata(device.Id, session.Id);

        // Assert
        var actionField = result.Fields.Single(f => f.Name == "action");
        actionField.Type.ShouldBe("enum");
        actionField.Values.ShouldContain(nameof(SyncRecordAction.CreateRemote));
        actionField.Values.ShouldContain(nameof(SyncRecordAction.Skipped));
    }

    #endregion

    #region GetSessionRecordsFilterValues

    [Fact]
    public async Task GetSessionRecordsFilterValues_FilePath_ReturnsSessionScopedPaths()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await controller.GetSessionRecordsFilterValues(device.Id, session.Id, field: "filePath", CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldContain("/a.mp3");
        result.Values.ShouldContain("/b.mp3");
    }

    [Fact]
    public async Task GetSessionRecordsFilterValues_SongTitle_ReturnsOwnerScopedTitles()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.CreateSong("Galaxy");
        scenario.CreateSong("iPad");

        // Act
        var result = await controller.GetSessionRecordsFilterValues(device.Id, session.Id, field: "song.title", CancellationToken.None);

        // Assert
        result.Values.ShouldContain("Galaxy");
        result.Values.ShouldContain("iPad");
    }

    [Fact]
    public async Task GetSessionRecordsFilterValues_UnknownField_ReturnsEmpty()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);

        // Act
        var result = await controller.GetSessionRecordsFilterValues(device.Id, session.Id, field: "unknownField", CancellationToken.None);

        // Assert
        result.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSessionRecordsFilterValues_WithSearch_FiltersResults()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/music/galaxy1.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/music/galaxy2.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/other.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await controller.GetSessionRecordsFilterValues(device.Id, session.Id, field: "filePath", CancellationToken.None, search: "galaxy");

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldAllBe(v => v.Contains("galaxy"));
    }

    [Fact]
    public async Task GetSessionRecordsFilterValues_RespectsLimit()
    {
        // Arrange
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/c.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await controller.GetSessionRecordsFilterValues(device.Id, session.Id, field: "filePath", CancellationToken.None, limit: 2);

        // Assert
        result.Values.Count.ShouldBe(2);
    }

    #endregion
}