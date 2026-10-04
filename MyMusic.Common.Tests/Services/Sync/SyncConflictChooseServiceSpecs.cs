using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncConflictChooseServiceSpecs
{
    private const string NamingTemplate = "{{ simple_label }}{{ extension }}";

    /// <summary>Cancels the Nth save after <see cref="FailOnSave"/> is set, as a client disconnect would.</summary>
    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public int? FailOnSave { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailOnSave != null && --FailOnSave == 0)
            {
                FailOnSave = null;
                throw new OperationCanceledException("Save cancelled by the test");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <param name="db">A context of its own, as each request has; defaults to the scenario's.</param>
    private static SyncConflictChooseService CreateService(Scenario scenario, MusicDbContext? db = null)
    {
        var config = Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = NamingTemplate,
        });
        return new SyncConflictChooseService(
            db ?? scenario.DbContext,
            new DeviceLookupService(),
            new SyncSessionLookupService(),
            new SyncActionsServerFactory(),
            new SyncPathResolver(),
            new SyncUsedPathsService(),
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

    private static void AddRename(Scenario scenario, long sessionId, string previousPath, string newPath, long songId) =>
        scenario.AddRecord(sessionId, newPath, SyncRecordAction.Rename,
            data: SyncActionDataSerializer.Serialize(new RenameData { PreviousPath = previousPath, NewPath = newPath }),
            songId: songId);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChooseDownloadAsync_TemplatePathTakenByAnEarlierRequestOfTheSession_GetsASuffix(bool isDryRun)
    {
        // Arrange: an earlier request (resolve-conflicts) already renamed the other copy to the template path
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: isDryRun);
        var song = scenario.CreateSong("Song");
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, "Copy A.mp3");
        scenario.CreateSongDevice(device, song, "Copy B.mp3");
        AddRename(scenario, session.Id, "Copy A.mp3", expectedPath, song.Id);
        var conflict = scenario.AddRecord(session.Id, "Copy B.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var rename = result.Records.Single(r => r.Action == SyncRecordAction.Rename);
        rename.FilePath.ShouldNotBe(expectedPath);
        rename.FilePath.ShouldBe(Path.ChangeExtension(expectedPath, null) + " (2)" + Path.GetExtension(expectedPath));
    }

    [Fact]
    public async Task ChooseDownloadAsync_DifferentSongsWithTheSameTemplatePath_InSeparateRequests_GetDifferentPaths()
    {
        // Arrange: two songs the template names the same way
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var first = scenario.CreateSong("Song");
        var second = scenario.CreateSong("Song", repositoryPath: "/music/second/Song.mp3");
        ComputeExpectedPath(second).ShouldBe(ComputeExpectedPath(first));
        scenario.CreateSongDevice(device, first, "First.mp3");
        scenario.CreateSongDevice(device, second, "Second.mp3");
        var firstConflict = scenario.AddRecord(session.Id, "First.mp3", SyncRecordAction.Conflict, songId: first.Id);
        var secondConflict = scenario.AddRecord(session.Id, "Second.mp3", SyncRecordAction.Conflict, songId: second.Id);

        // Act: each choice is its own request
        var firstResult = await CreateService(scenario).ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [firstConflict.Id], CancellationToken.None);
        var secondResult = await CreateService(scenario).ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [secondConflict.Id], CancellationToken.None);

        // Assert
        var firstRename = firstResult!.Records.Single(r => r.Action == SyncRecordAction.Rename);
        var secondRename = secondResult!.Records.Single(r => r.Action == SyncRecordAction.Rename);
        firstRename.FilePath.ShouldBe(ComputeExpectedPath(first));
        secondRename.FilePath.ShouldNotBe(firstRename.FilePath);
    }

    [Fact]
    public async Task ChooseDownloadAsync_TemplatePathFreedByAnEarlierRename_IsUsedWithoutASuffix()
    {
        // Arrange: another file held the template path, and an earlier request renamed it away
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        var other = scenario.CreateSong("Other");
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, other, expectedPath);
        scenario.CreateSongDevice(device, song, "Copy.mp3");
        AddRename(scenario, session.Id, expectedPath, ComputeExpectedPath(other), other.Id);
        var conflict = scenario.AddRecord(session.Id, "Copy.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None);

        // Assert
        result!.Records.Single(r => r.Action == SyncRecordAction.Rename).FilePath.ShouldBe(expectedPath);
    }

    [Fact]
    public async Task ChooseDownloadAsync_PathFreedByARenameOfTheSameRequest_IsUsedByTheNextFile()
    {
        // Arrange: the first file leaves the path the template gives the second one
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        var other = scenario.CreateSong("Other");
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, other, expectedPath);
        scenario.CreateSongDevice(device, song, "Copy.mp3");
        var first = scenario.AddRecord(session.Id, expectedPath, SyncRecordAction.Conflict, songId: other.Id);
        var second = scenario.AddRecord(session.Id, "Copy.mp3", SyncRecordAction.Conflict, songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [first.Id, second.Id], CancellationToken.None);

        // Assert
        var renames = result!.Records.Where(r => r.Action == SyncRecordAction.Rename).ToList();
        renames.Select(r => r.FilePath).ShouldBe([ComputeExpectedPath(other), expectedPath]);
    }

    /// <summary>
    /// A session with one conflict whose file the template renames, so choosing the server version saves
    /// three times: the <c>UpdateLocal</c>, the <c>Rename</c>, and the links to the conflict.
    /// </summary>
    private static (Device Device, DeviceSyncSession Session, Song Song, DeviceSyncSessionRecord Conflict) CreateRenamedConflict(Scenario scenario)
    {
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device);
        var song = scenario.CreateSong("Song");
        scenario.CreateSongDevice(device, song, "OldName.mp3");
        var conflict = scenario.AddRecord(session.Id, "OldName.mp3", SyncRecordAction.Conflict, songId: song.Id);
        return (device, session, song, conflict);
    }

    private static async Task FailChooseDownloadAsync(
        Scenario scenario, SaveFailure saveFailure, int failOnSave, Device device, DeviceSyncSession session, DeviceSyncSessionRecord conflict)
    {
        await using var db = await scenario.DbContextFactory.CreateDbContextAsync();
        var service = CreateService(scenario, db);
        saveFailure.FailOnSave = failOnSave;

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None));
    }

    [Theory]
    // The Rename insert
    [InlineData(2)]
    // The save that links the records to the conflict
    [InlineData(3)]
    public async Task ChooseDownloadAsync_SaveFails_LeavesNoRecords(int failOnSave)
    {
        // Arrange
        var saveFailure = new SaveFailure();
        var scenario = new Scenario(saveFailure);
        var (device, session, _, conflict) = CreateRenamedConflict(scenario);

        // Act
        await FailChooseDownloadAsync(scenario, saveFailure, failOnSave, device, session, conflict);

        // Assert: only the conflict is left in the session
        var saved = await scenario.DbContext.DeviceSyncSessionRecords.AsNoTracking()
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        saved.ShouldHaveSingleItem().Id.ShouldBe(conflict.Id);
    }

    [Fact]
    public async Task ChooseDownloadAsync_SaveFails_SessionCanStillBeCommitted()
    {
        // Arrange
        var saveFailure = new SaveFailure();
        var scenario = new Scenario(saveFailure);
        var (device, session, _, conflict) = CreateRenamedConflict(scenario);
        await FailChooseDownloadAsync(scenario, saveFailure, 3, device, session, conflict);
        var commitService = new SyncCommitService(
            scenario.FileSystem,
            Substitute.For<IMusicService>(),
            Substitute.For<ISyncSoundalikeMatcher>(),
            Substitute.For<ILoggerFactory>(),
            Substitute.For<ILogger<SyncCommitService>>());

        // Act & Assert: no undelivered client-action records are left to block the commit
        await using var db = await scenario.DbContextFactory.CreateDbContextAsync();
        await Should.NotThrowAsync(() => commitService.CommitAsync(db, session.Id, device.Id, session.IsDryRun));
    }

    [Fact]
    public async Task ChooseDownloadAsync_AfterAFailedRequest_ResolvesTheConflictAsIfItWereTheFirst()
    {
        // Arrange
        var saveFailure = new SaveFailure();
        var scenario = new Scenario(saveFailure);
        var (device, session, song, conflict) = CreateRenamedConflict(scenario);
        await FailChooseDownloadAsync(scenario, saveFailure, 3, device, session, conflict);

        // Act
        await using var db = await scenario.DbContextFactory.CreateDbContextAsync();
        var result = await CreateService(scenario, db).ChooseDownloadAsync(device.Id, session.Id, scenario.AdminUser.Id, [conflict.Id], CancellationToken.None);

        // Assert: the template path was not left taken by the failed request
        result.ShouldNotBeNull();
        result.Records.Select(r => r.Action).ShouldBe([SyncRecordAction.UpdateLocal, SyncRecordAction.Rename]);
        result.Records[1].FilePath.ShouldBe(ComputeExpectedPath(song));
        result.Records.ShouldAllBe(r => r.ResolvesConflictRecordId == conflict.Id);

        var saved = await scenario.DbContext.DeviceSyncSessionRecords.AsNoTracking()
            .Where(r => r.SessionId == session.Id && r.Action != SyncRecordAction.Conflict)
            .ToListAsync();
        saved.Count.ShouldBe(2);
        saved.ShouldAllBe(r => r.ResolvesConflictRecordId == conflict.Id);
    }
}
