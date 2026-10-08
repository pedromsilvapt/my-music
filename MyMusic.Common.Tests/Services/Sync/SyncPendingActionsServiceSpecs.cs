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

public class SyncPendingActionsServiceSpecs
{
    private const string NamingTemplate = "{{ simple_label }}{{ extension }}";

    private static SyncPendingActionsService CreateService(Scenario scenario)
    {
        var config = Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = NamingTemplate,
        });
        return new SyncPendingActionsService(
            scenario.DbContext,
            new DeviceLookupService(),
            new SyncSessionLookupService(),
            new SyncPathResolver(),
            new SyncUsedPathsService(),
            config,
            Substitute.For<ILogger<SyncPendingActionsService>>());
    }

    private static string ComputeExpectedPath(Song song)
    {
        var namingStrategy = new TemplateNamingStrategy(NamingTemplate);
        var metadata = EntityConverter.ToSong(song);
        var naming = NamingMetadata.FromPath(song.RepositoryPath);
        return namingStrategy.Generate(metadata, naming);
    }

    [Fact]
    public async Task CreateAsync_DeviceNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var session = scenario.CreateSession(scenario.CreateDevice(), status: SyncSessionStatus.InProgress);

        // Act
        var result = await service.CreateAsync(9999, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(otherDevice.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
        scenario.DbContext.DeviceSyncSessionRecords.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_DownloadSyncAction_CreatesCreateLocalRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        result.Records[0].FilePath.ShouldBe(expectedPath);
        result.Records[0].SongId.ShouldBe(song.Id);
        result.Records[0].Acknowledged.ShouldBeFalse();

        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
        records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        records[0].Acknowledged.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_DownloadSyncAction_RecordCarriesTheSongDates()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        song.CreatedAt = new DateTime(2019, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        song.FileModifiedAt = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        song.ModifiedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await scenario.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        scenario.CreateSongDevice(device, song, ComputeExpectedPath(song), syncAction: SongSyncAction.Download);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert: the file's date, not the date of the last metadata edit
        var data = result!.Records.Single().Data!.Value;
        data.GetProperty("serverModifiedAt").GetString().ShouldBe(song.FileModifiedAt.Value.ToString("O"));
        data.GetProperty("serverCreatedAt").GetString().ShouldBe(song.CreatedAt.ToString("O"));
    }

    [Fact]
    public async Task CreateAsync_SessionNamingTemplate_TakesPrecedenceOverDeviceTemplate()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice(namingTemplate: "device/{{ simple_label }}{{ extension }}");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, isDryRun: true,
            namingTemplate: "session/{{ simple_label }}{{ extension }}");
        var songDevice = scenario.CreateSongDevice(device, song, "Old.mp3", syncAction: SongSyncAction.Download);
        songDevice.LastSyncedModifiedAt = DateTime.UtcNow;
        scenario.DbContext.SaveChanges();

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var rename = result.Records.Single(r => r.Action == SyncRecordAction.Rename);
        rename.FilePath.ShouldStartWith("session/");
    }

    [Fact]
    public async Task CreateAsync_SessionWithoutNamingTemplate_FallsBackToDeviceTemplate()
    {
        // Arrange: sessions started before the template was recorded on them have none
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice(namingTemplate: "device/{{ simple_label }}{{ extension }}");
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device);
        var songDevice = scenario.CreateSongDevice(device, song, "Old.mp3", syncAction: SongSyncAction.Download);
        songDevice.LastSyncedModifiedAt = DateTime.UtcNow;
        scenario.DbContext.SaveChanges();

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var rename = result.Records.Single(r => r.Action == SyncRecordAction.Rename);
        rename.FilePath.ShouldStartWith("device/");
    }

    [Fact]
    public async Task CreateAsync_RemoveSyncAction_CreatesDeleteLocalRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Remove);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.DeleteLocal);
        result.Records[0].FilePath.ShouldBe(expectedPath);
    }

    [Fact]
    public async Task CreateAsync_DownloadWithPreviousSync_CreatesUpdateLocalRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        var songDevice = scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);
        songDevice.LastSyncedModifiedAt = DateTime.UtcNow;
        scenario.DbContext.SaveChanges();

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.UpdateLocal);
    }

    [Fact]
    public async Task CreateAsync_PathChanged_CreatesCreateLocalRecordWithNewPath()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        scenario.CreateSongDevice(device, song, "OldPath.mp3", syncAction: SongSyncAction.Download);
        var expectedNewPath = ComputeExpectedPath(song);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        result.Records[0].FilePath.ShouldBe(expectedNewPath);
    }

    [Fact]
    public async Task CreateAsync_CalledTwice_DoesNotCreateDuplicateRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);

        // Act
        await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);
        await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        var records = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        records.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_DownloadNeverSynced_LinkedAtAnotherPath_CreatesUnlinkRecord()
    {
        // Arrange - a local copy of the song was linked by checksum at another path, so the pending
        // download at the SongDevice's path is redundant
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);
        scenario.AddRecord(session.Id, "Song (Explicit).mp3", SyncRecordAction.Link, songId: song.Id, acknowledged: true);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.Unlink);
        result.Records[0].FilePath.ShouldBe(expectedPath);
        result.Records[0].SongId.ShouldBe(song.Id);
        result.Records[0].Reason!.ShouldContain("Song (Explicit).mp3");
    }

    [Fact]
    public async Task CreateAsync_DownloadPreviouslySynced_LinkedAtAnotherPath_CreatesUnlinkRecord()
    {
        // Arrange - the SongDevice was synced before, but its file was replaced by a linked copy at another path
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var songDevice = scenario.CreateSongDevice(device, song, "OldPath.mp3", syncAction: SongSyncAction.Download);
        songDevice.LastSyncedModifiedAt = DateTime.UtcNow;
        scenario.DbContext.SaveChanges();
        scenario.AddRecord(session.Id, "Song (Explicit).mp3", SyncRecordAction.Link, songId: song.Id, acknowledged: true);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.Unlink);
        result.Records[0].FilePath.ShouldBe("OldPath.mp3");
        result.Records[0].SongId.ShouldBe(song.Id);
        result.Records[0].Reason!.ShouldContain("Song (Explicit).mp3");
    }

    [Theory]
    [InlineData(SyncRecordAction.CreateRemote)]
    [InlineData(SyncRecordAction.Skipped)]
    [InlineData(SyncRecordAction.UpdateLocal)]
    public async Task CreateAsync_RecordForSameSongAtAnotherPath_StillCreatesDownloadRecord(SyncRecordAction otherAction)
    {
        // Arrange - another local file with the same song (e.g. an older version replaced by an UpdateLocal)
        // must not hide the pending download of the SongDevice at its own path
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);
        scenario.AddRecord(session.Id, "Song (Explicit).mp3", otherAction, songId: song.Id, acknowledged: true);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.CreateLocal);
        result.Records[0].FilePath.ShouldBe(expectedPath);
        result.Records[0].SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task CreateAsync_RecordAtSongDevicePath_CreatesNoRecords()
    {
        // Arrange - the check phase already handled the SongDevice's path
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Download);
        scenario.AddRecord(session.Id, expectedPath, SyncRecordAction.Skipped, songId: song.Id, acknowledged: true);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_UploadSyncAction_CreatesNoRecords()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Upload);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_DeviceNamingTemplateIsNull_FallsBackToDefaultTemplate()
    {
        // Arrange - device has no explicit NamingTemplate; service should use config default.
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var device = scenario.CreateDevice(); // NamingTemplate is null
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, "Different.mp3", syncAction: SongSyncAction.Download);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].FilePath.ShouldBe(expectedPath);
    }

    [Fact]
    public async Task CreateAsync_TemplatePathTakenByAnEarlierRename_GetsASuffix()
    {
        // Arrange: an earlier request of the session renamed another copy to the template path
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, "Copy A.mp3");
        scenario.AddRecord(session.Id, expectedPath, SyncRecordAction.Rename,
            data: SyncActionDataSerializer.Serialize(new RenameData { PreviousPath = "Copy A.mp3", NewPath = expectedPath }),
            songId: song.Id);
        scenario.CreateSongDevice(device, song, "Copy B.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Download);
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        var rename = result!.Records.Single(r => r.Action == SyncRecordAction.Rename);
        rename.FilePath.ShouldNotBe(expectedPath);
    }

    [Fact]
    public async Task CreateAsync_TemplatePathFreedByAnEarlierDeleteLocal_IsUsedWithoutASuffix()
    {
        // Arrange: the file at the template path is deleted by a record of an earlier request
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath, syncAction: SongSyncAction.Remove);
        scenario.AddRecord(session.Id, expectedPath, SyncRecordAction.DeleteLocal, songId: song.Id);
        scenario.CreateSongDevice(device, song, "Copy.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Download);
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result!.Records.Single(r => r.Action == SyncRecordAction.Rename).FilePath.ShouldBe(expectedPath);
    }

    [Fact]
    public async Task CreateAsync_TemplatePathOfASongDeviceMarkedForRemoval_IsUsedWithoutASuffix()
    {
        // Arrange: the file at the template path is to be deleted, and has no DeleteLocal record yet
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, "Copy.mp3", lastSyncedModifiedAt: DateTime.UtcNow, syncAction: SongSyncAction.Download);
        scenario.CreateSongDevice(device, null, expectedPath, syncAction: SongSyncAction.Remove);
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert: whichever SongDevice is handled first, the removed file's path goes to the renamed one
        result!.Records.Single(r => r.Action == SyncRecordAction.Rename).FilePath.ShouldBe(expectedPath);
        result.Records.Single(r => r.Action == SyncRecordAction.DeleteLocal).FilePath.ShouldBe(expectedPath);
    }

    [Theory]
    [InlineData(SyncRecordAction.Rename)]
    [InlineData(SyncRecordAction.CreateLocal)]
    public async Task CreateAsync_RemovedSongDevicePathGivenToAnotherFile_StillCreatesDeleteLocalRecord(SyncRecordAction otherAction)
    {
        // Arrange: an earlier request of the session gave the path of the removed file to another song's file
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        scenario.CreateSongDevice(device, null, "Removed.mp3", syncAction: SongSyncAction.Remove);
        scenario.AddRecord(session.Id, "Removed.mp3", otherAction,
            data: SyncActionDataSerializer.Serialize(new RenameData { PreviousPath = "Kept.mp3", NewPath = "Removed.mp3" }),
            songId: song.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result!.Records.ShouldHaveSingleItem().Action.ShouldBe(SyncRecordAction.DeleteLocal);
        result.Records[0].FilePath.ShouldBe("Removed.mp3");
    }

    private static SongDevice CreateSyncedSongDeviceWithRequestedPath(Scenario scenario, Device device, Song song, string devicePath, string requestedPath)
    {
        var songDevice = scenario.CreateSongDevice(device, song, devicePath, lastSyncedModifiedAt: DateTime.UtcNow);
        songDevice.RequestedPath = requestedPath;
        scenario.DbContext.SaveChanges();
        return songDevice;
    }

    [Fact]
    public async Task CreateAsync_RequestedPathOfUnchangedSong_CreatesRenameRecord()
    {
        // Arrange: the device reported the file, which is unchanged (Skipped), and the user typed a path for it
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        CreateSyncedSongDeviceWithRequestedPath(scenario, device, song, "Old/Song.mp3", "Custom/Typed name.mp3");
        scenario.AddRecord(session.Id, "Old/Song.mp3", SyncRecordAction.Skipped, songId: song.Id, acknowledged: true);
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert: the typed path is used as is, not the naming template's
        var rename = result!.Records.ShouldHaveSingleItem();
        rename.Action.ShouldBe(SyncRecordAction.Rename);
        rename.FilePath.ShouldBe("Custom/Typed name.mp3");
        rename.SongId.ShouldBe(song.Id);
        var data = SyncActionDataSerializer.Deserialize<RenameData>(rename.Data);
        data!.PreviousPath.ShouldBe("Old/Song.mp3");
        data.NewPath.ShouldBe("Custom/Typed name.mp3");
    }

    [Fact]
    public async Task CreateAsync_RequestedPathAlreadyRenamedInSession_DoesNotCreateAnotherRename()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        CreateSyncedSongDeviceWithRequestedPath(scenario, device, song, "Old/Song.mp3", "Custom/Song.mp3");
        scenario.AddRecord(session.Id, "Old/Song.mp3", SyncRecordAction.Skipped, songId: song.Id, acknowledged: true);
        var service = CreateService(scenario);
        await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result!.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_RequestedPathOfFileNotReportedByDevice_CreatesNoRecord()
    {
        // Arrange: no record at the SongDevice's path, so the device does not hold the file
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        CreateSyncedSongDeviceWithRequestedPath(scenario, device, song, "Old/Song.mp3", "Custom/Song.mp3");
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result!.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_RequestedPathInDirectionUp_CreatesNoRecord()
    {
        // Arrange: in `up` the device processes no server actions
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, direction: SyncDirection.Up);
        CreateSyncedSongDeviceWithRequestedPath(scenario, device, song, "Old/Song.mp3", "Custom/Song.mp3");
        scenario.AddRecord(session.Id, "Old/Song.mp3", SyncRecordAction.Skipped, songId: song.Id, acknowledged: true);
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        result!.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_RequestedPathOfNeverDownloadedSong_CreatesCreateLocalAtRequestedPath()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var songDevice = scenario.CreateSongDevice(device, song, "Custom/Typed name.mp3", syncAction: SongSyncAction.Download);
        songDevice.RequestedPath = "Custom/Typed name.mp3";
        scenario.DbContext.SaveChanges();
        var service = CreateService(scenario);

        // Act
        var result = await service.CreateAsync(device.Id, session.Id, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        var record = result!.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.CreateLocal);
        record.FilePath.ShouldBe("Custom/Typed name.mp3");
    }
}
