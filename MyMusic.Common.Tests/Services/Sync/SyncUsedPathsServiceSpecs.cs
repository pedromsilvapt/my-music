using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncUsedPathsServiceSpecs
{
    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static void AddRename(Scenario scenario, long sessionId, string previousPath, string newPath) =>
        scenario.AddRecord(sessionId, newPath, SyncRecordAction.Rename,
            data: SyncActionDataSerializer.Serialize(new RenameData { PreviousPath = previousPath, NewPath = newPath }));

    [Fact]
    public async Task GetAsync_NoSessionRecords_ReturnsThePathsOfTheDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var otherDevice = scenario.CreateDevice("Other");
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "A.mp3");
        scenario.CreateSongDevice(otherDevice, song, "B.mp3");

        // Act
        var usedPaths = await new SyncUsedPathsService().GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        usedPaths.Contains("A.mp3").ShouldBeTrue();
        usedPaths.Contains("B.mp3").ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_RenameRecord_FreesThePreviousPathAndTakesTheNewPath()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        scenario.CreateSongDevice(device, scenario.CreateSong("Song"), "Old.mp3");
        AddRename(scenario, session.Id, "Old.mp3", "New.mp3");

        // Act
        var usedPaths = await new SyncUsedPathsService().GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        usedPaths.Contains("Old.mp3").ShouldBeFalse();
        usedPaths.Contains("New.mp3").ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_CreateLocalRecord_TakesItsPath()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        scenario.AddRecord(session.Id, "Created.mp3", SyncRecordAction.CreateLocal);

        // Act
        var usedPaths = await new SyncUsedPathsService().GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        usedPaths.Contains("Created.mp3").ShouldBeTrue();
    }

    [Theory]
    [InlineData(SyncDirection.Both)]
    [InlineData(SyncDirection.Down)]
    public async Task GetAsync_SongDeviceMarkedForRemoval_ItsPathIsFree(SyncDirection direction)
    {
        // Arrange: the session will delete the file, whether or not its DeleteLocal record exists yet
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, direction: direction);
        scenario.CreateSongDevice(device, scenario.CreateSong("Song"), "Removed.mp3", syncAction: SongSyncAction.Remove);

        // Act
        var usedPaths = await new SyncUsedPathsService().GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        usedPaths.Contains("Removed.mp3").ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_SongDeviceMarkedForRemoval_DirectionUp_ItsPathIsTaken()
    {
        // Arrange: in `up` nothing is deleted from the device
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, direction: SyncDirection.Up);
        scenario.CreateSongDevice(device, scenario.CreateSong("Song"), "Removed.mp3", syncAction: SongSyncAction.Remove);

        // Act
        var usedPaths = await new SyncUsedPathsService().GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        usedPaths.Contains("Removed.mp3").ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_DeleteLocalRecordAfterARenameToItsPath_ThePathStaysTaken()
    {
        // Arrange: the path of the removed file was given to another file before its DeleteLocal was created
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, null, "Removed.mp3", syncAction: SongSyncAction.Remove);
        scenario.CreateSongDevice(device, song, "Kept.mp3");
        AddRename(scenario, session.Id, "Kept.mp3", "Removed.mp3");
        scenario.AddRecord(session.Id, "Removed.mp3", SyncRecordAction.DeleteLocal);

        // Act
        var usedPaths = await new SyncUsedPathsService().GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        usedPaths.Contains("Removed.mp3").ShouldBeTrue();
        usedPaths.Contains("Kept.mp3").ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_PathFreedAndThenTakenAgain_IsTaken()
    {
        // Arrange: records are applied in the order they were created
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "A.mp3");
        scenario.CreateSongDevice(device, song, "B.mp3");
        AddRename(scenario, session.Id, "A.mp3", "C.mp3");
        AddRename(scenario, session.Id, "B.mp3", "A.mp3");

        // Act
        var usedPaths = await new SyncUsedPathsService().GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        usedPaths.Contains("A.mp3").ShouldBeTrue();
        usedPaths.Contains("B.mp3").ShouldBeFalse();
        usedPaths.Contains("C.mp3").ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_RecordsOfAnotherSession_AreIgnored()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var otherSession = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled);
        scenario.CreateSongDevice(device, scenario.CreateSong("Song"), "Old.mp3");
        AddRename(scenario, otherSession.Id, "Old.mp3", "New.mp3");

        // Act
        var usedPaths = await new SyncUsedPathsService().GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        usedPaths.Contains("Old.mp3").ShouldBeTrue();
        usedPaths.Contains("New.mp3").ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_CalledAgainInTheSameRequest_ReturnsTheSameInstanceWithoutQuerying()
    {
        // Arrange
        var counter = new QueryCounter();
        var scenario = new Scenario(counter);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        scenario.CreateSongDevice(device, scenario.CreateSong("Song"), "A.mp3");
        var service = new SyncUsedPathsService();

        // Act
        var first = await service.GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);
        var queriesAfterFirst = counter.Count;
        var second = await service.GetAsync(scenario.DbContext, device.Id, session.Id, CancellationToken.None);

        // Assert
        second.ShouldBeSameAs(first);
        counter.Count.ShouldBe(queriesAfterFirst);
    }
}
