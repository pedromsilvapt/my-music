using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncConflictChooseServiceSpecs
{
    private const string NamingTemplate = "{{ simple_label }}{{ extension }}";

    private static SyncConflictChooseService CreateService(Scenario scenario)
    {
        var config = Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = NamingTemplate,
        });
        return new SyncConflictChooseService(
            scenario.DbContext,
            new DeviceLookupService(),
            new SyncSessionLookupService(),
            new SyncActionsServerFactory(),
            new SyncPathResolver(),
            config,
            Substitute.For<ILogger<SyncConflictChooseService>>());
    }

    private static string ComputeExpectedPath(Song song)
    {
        var namingStrategy = new TemplateNamingStrategy(NamingTemplate);
        var metadata = EntityConverter.ToSong(song);
        var naming = NamingMetadata.FromPath(song.RepositoryPath);
        return namingStrategy.Generate(metadata, naming);
    }

    [Fact]
    public async Task ChooseDownloadAsync_DeviceNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var session = scenario.CreateSession(scenario.CreateDevice());
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(9999, session.Id, scenario.AdminUser.Id, [], CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ChooseDownloadAsync_SessionNotInProgress_Throws()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var service = CreateService(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [], CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChooseDownloadAsync_Conflict_CreatesUpdateLocalRecordResolvingIt(bool isDryRun)
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: isDryRun);
        var song = scenario.CreateSong("Song");
        var path = ComputeExpectedPath(song);
        var songDevice = scenario.CreateSongDevice(device, song, path, lastSyncedModifiedAt: DateTime.UtcNow.AddDays(-1));
        var lastSynced = songDevice.LastSyncedModifiedAt;
        var conflict = scenario.AddRecord(session.Id, path, SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.UpdateLocal);
        record.FilePath.ShouldBe(path);
        record.SongId.ShouldBe(song.Id);
        record.ResolvesConflictRecordId.ShouldBe(conflict.Id);

        var saved = await scenario.DbContext.DeviceSyncSessionRecords.AsNoTracking()
            .SingleAsync(r => r.SessionId == session.Id && r.Action == SyncRecordAction.UpdateLocal);
        saved.ResolvesConflictRecordId.ShouldBe(conflict.Id);

        // Only the commit of a real run changes the SongDevice
        (await scenario.DbContext.SongDevices.AsNoTracking().SingleAsync(sd => sd.DevicePath == path))
            .LastSyncedModifiedAt.ShouldBe(lastSynced);
    }

    [Fact]
    public async Task ChooseDownloadAsync_PathChangedByNamingTemplate_AlsoCreatesRenameRecordResolvingIt()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "OldName.mp3");
        var conflict = scenario.AddRecord(session.Id, "OldName.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Select(r => r.Action).ShouldBe([SyncRecordAction.UpdateLocal, SyncRecordAction.Rename]);
        result.Records[0].FilePath.ShouldBe("OldName.mp3");
        result.Records[1].FilePath.ShouldBe(ComputeExpectedPath(song));
        result.Records.ShouldAllBe(r => r.ResolvesConflictRecordId == conflict.Id);
        result.Records.CountUnresolvedConflicts().ShouldBe(-1);
    }

    [Fact]
    public async Task ChooseDownloadAsync_DirectionUp_CreatesSkippedRecordAndLeavesConflictUnresolved()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, direction: SyncDirection.Up);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "song.mp3");
        var conflict = scenario.AddRecord(session.Id, "song.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Skipped);
        record.ResolvesConflictRecordId.ShouldBeNull();
    }

    [Fact]
    public async Task ChooseDownloadAsync_AlreadyResolvedConflict_IsIgnored()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "song.mp3");
        var conflict = scenario.AddRecord(session.Id, "song.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);
        await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChooseDownloadAsync_IdsThatAreNotConflictsOfTheSession_AreIgnored()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var otherSession = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "song.mp3");
        var skipped = scenario.AddRecord(session.Id, "song.mp3", SyncRecordAction.Skipped, songId: song.Id);
        var otherConflict = scenario.AddRecord(otherSession.Id, "song.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [skipped.Id, otherConflict.Id, 9999], CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChooseDownloadAsync_SongAtTwoConflictedPaths_ResolvesEachAtItsOwnPath()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "Copy A.mp3");
        scenario.CreateSongDevice(device, song, "Copy B.mp3");
        var first = scenario.AddRecord(session.Id, "Copy A.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var second = scenario.AddRecord(session.Id, "Copy B.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [first.Id, second.Id], CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var updates = result.Records.Where(r => r.Action == SyncRecordAction.UpdateLocal).ToList();
        updates.Select(r => r.FilePath).ShouldBe(["Copy A.mp3", "Copy B.mp3"]);
        updates.Select(r => r.ResolvesConflictRecordId).ShouldBe([first.Id, second.Id]);

        // Both copies are renamed by the template, to different paths
        var renames = result.Records.Where(r => r.Action == SyncRecordAction.Rename).ToList();
        renames.Select(r => r.FilePath).Distinct().Count().ShouldBe(2);
        result.Records.CountUnresolvedConflicts().ShouldBe(-2);
    }
}
