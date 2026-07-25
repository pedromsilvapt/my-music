using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncSessionFilterValuesServiceSpecs
{
    private static SyncSessionFilterValuesService CreateService(Scenario scenario) => new(scenario.DbContext);

    [Fact]
    public async Task Get_FilePath_ReturnsDistinctOwnerAndSessionScopedPaths()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.Skipped); // duplicate path
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, device.Id, session.Id, "filePath", null, 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldContain("/a.mp3");
        result.Values.ShouldContain("/b.mp3");
    }

    [Fact]
    public async Task Get_FilePath_OtherUsersRecordsNotIncluded()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var otherSession = scenario.CreateSession(otherDevice, status: SyncSessionStatus.Completed);
        scenario.AddRecord(otherSession.Id, "/theirs.mp3", SyncRecordAction.CreateRemote);

        var device = scenario.CreateDevice("Mine");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/mine.mp3", SyncRecordAction.CreateRemote);

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, device.Id, session.Id, "filePath", null, 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(1);
        result.Values[0].ShouldBe("/mine.mp3");
    }

    [Fact]
    public async Task Get_SongTitle_ReturnsDistinctOwnerScopedTitles()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        scenario.CreateSong("Galaxy", ownerId: scenario.AdminUser.Id);
        scenario.CreateSong("Galaxy", ownerId: scenario.AdminUser.Id); // duplicate title
        scenario.CreateSong("iPad", ownerId: scenario.AdminUser.Id);
        scenario.CreateSong("Theirs", ownerId: otherUser.Id);

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, 1, 1, "song.title", null, 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldContain("Galaxy");
        result.Values.ShouldContain("iPad");
        result.Values.ShouldNotContain("Theirs");
    }

    [Fact]
    public async Task Get_SongArtistName_ReturnsDistinctOwnerScopedArtistNames()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        scenario.CreateSong("A"); // creates artist "A Artist"
        scenario.CreateSong("B"); // creates artist "B Artist"
        scenario.CreateSong("T", ownerId: otherUser.Id); // creates artist "T Artist" for other user

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, 1, 1, "song.artist.name", null, 15, CancellationToken.None);

        // Assert
        result.Values.ShouldContain("A Artist");
        result.Values.ShouldContain("B Artist");
        result.Values.ShouldNotContain("T Artist");
    }

    [Fact]
    public async Task Get_SongAlbumName_ReturnsDistinctOwnerScopedAlbumNames()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateSong("A"); // creates album "A Album"
        scenario.CreateSong("B"); // creates album "B Album"

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, 1, 1, "song.album.name", null, 15, CancellationToken.None);

        // Assert
        result.Values.ShouldContain("A Album");
        result.Values.ShouldContain("B Album");
    }

    [Fact]
    public async Task Get_UnknownField_ReturnsEmpty()
    {
        // Arrange
        var scenario = new Scenario();

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, 1, 1, "unknownField", null, 15, CancellationToken.None);

        // Assert
        result.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_WithSearch_FiltersCaseInsensitiveContains()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/music/galaxy1.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/music/galaxy2.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/other.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, device.Id, session.Id, "filePath", "GALAXY", 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldAllBe(v => v.Contains("galaxy"));
    }

    [Fact]
    public async Task Get_Limit_CapsResults()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/a.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/b.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/c.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/d.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, device.Id, session.Id, "filePath", null, 2, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Get_OrdersAscending()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        scenario.AddRecord(session.Id, "/zeta.mp3", SyncRecordAction.CreateRemote);
        scenario.AddRecord(session.Id, "/alpha.mp3", SyncRecordAction.Skipped);
        scenario.AddRecord(session.Id, "/mid.mp3", SyncRecordAction.Skipped);

        // Act
        var result = await CreateService(scenario).GetAsync(
            scenario.AdminUser.Id, device.Id, session.Id, "filePath", null, 15, CancellationToken.None);

        // Assert
        result.Values.ShouldBe(["/alpha.mp3", "/mid.mp3", "/zeta.mp3"]);
    }
}