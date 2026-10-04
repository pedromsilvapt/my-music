using System.IO.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

public class SyncResolveConflictsServiceSpecs
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

    private static SyncResolveConflictsService CreateService(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var config = Options.Create(new Config
        {
            MusicRepositoryPath = "/music",
            DefaultNamingTemplate = NamingTemplate,
        });
        return new SyncResolveConflictsService(
            scenario.DbContext,
            new DeviceLookupService(),
            new SyncSessionLookupService(),
            factory ?? new SyncActionsServerFactory(),
            new SyncPathResolver(),
            new SyncUsedPathsService(),
            config,
            Substitute.For<ILogger<SyncResolveConflictsService>>());
    }

    private static string ComputeChecksum(byte[] content)
    {
        var xxHash = new XxHash128();
        xxHash.Append(content);
        return Convert.ToBase64String(xxHash.GetCurrentHash());
    }

    private static string ComputeExpectedPath(Song song)
    {
        var namingStrategy = new TemplateNamingStrategy(NamingTemplate);
        var metadata = EntityConverter.ToSong(song);
        var naming = NamingMetadata.FromPath(song.RepositoryPath);
        return namingStrategy.Generate(metadata, naming);
    }

    private static SyncResolveConflictsInput InputFor(
        List<SyncResolveConflictItem>? conflicts = null,
        List<SyncResolvePotentialUpdateItem>? potentialUpdates = null) =>
        new()
        {
            Conflicts = conflicts ?? [],
            PotentialUpdates = potentialUpdates ?? [],
        };

    [Fact]
    public async Task ResolveAsync_DeviceNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);
        var session = scenario.CreateSession(scenario.CreateDevice(), status: SyncSessionStatus.InProgress);

        // Act
        var result = await service.ResolveAsync(9999, session.Id, scenario.AdminUser.Id, InputFor(), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var session = scenario.CreateSession(otherDevice, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        // Act
        var result = await service.ResolveAsync(otherDevice.Id, session.Id, scenario.AdminUser.Id, InputFor(), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var service = CreateService(scenario);

        // Act
        var result = await service.ResolveAsync(device.Id, 0, scenario.AdminUser.Id, InputFor(), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_SessionNotInProgress_Throws()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.Completed);
        var service = CreateService(scenario);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() =>
            service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, InputFor(), CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_ConflictChecksumsMatch_CreatesUpdateTimestampRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(content),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
        result.Records[0].SongId.ShouldBe(song.Id);

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        dbRecords.Count.ShouldBe(1);
        dbRecords[0].Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
    }

    [Fact]
    public async Task ResolveAsync_ConflictChecksumsDiffer_CreatesConflictRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.Conflict);
        result.Records[0].SongId.ShouldBe(song.Id);
        result.Records[0].FilePath.ShouldBe("/music/song.mp3");

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id && r.Action == SyncRecordAction.Conflict)
            .ToListAsync();
        dbRecords.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ResolveAsync_ConflictLocalIsPreviousVersion_ServerWinsWithUpdateLocalRecord()
    {
        // Arrange: the local file matches an older version of the song, so it is a stale copy
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        scenario.AddChecksumHistory(song, ComputeChecksum(clientContent));
        var devicePath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, devicePath);

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = devicePath,
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateLocal);
        record.SongId.ShouldBe(song.Id);
        record.FilePath.ShouldBe(devicePath);
    }

    [Fact]
    public async Task ResolveAsync_DirectionUp_ConflictLocalIsPreviousVersion_CreatesSkippedRecord()
    {
        // Arrange: a stale local copy, but in `up` the device never downloads
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, direction: SyncDirection.Up);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        scenario.AddChecksumHistory(song, ComputeChecksum(clientContent));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Skipped);
        record.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task ResolveAsync_ConflictInvalidBase64_CreatesErrorRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = "not-valid-base64!!!",
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.Error);
        result.Records[0].Reason.ShouldBe("Invalid file content format");
    }

    [Fact]
    public async Task ResolveAsync_ConflictSongDeviceNotFound_SkipsNoRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        // No SongDevice created for this song on this device.

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(content),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.ShouldBeEmpty();

        var dbRecords = await scenario.DbContext.DeviceSyncSessionRecords
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        dbRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateChecksumsMatch_CreatesUpdateTimestampRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 10, 20, 30 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(content),
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
        result.Records[0].SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateChecksumsDiffer_CreatesUpdateLocalRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        // Device path matches the template-generated path so no rename is produced.
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath);

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = expectedPath,
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var updateLocalRecords = result.Records.Where(r => r.Action == SyncRecordAction.UpdateLocal).ToList();
        updateLocalRecords.Count.ShouldBe(1);
        updateLocalRecords[0].SongId.ShouldBe(song.Id);

        // No rename because the device path already matches the template-generated path.
        result.Records.ShouldNotContain(r => r.Action == SyncRecordAction.Rename);
    }

    [Fact]
    public async Task ResolveAsync_SongLinkedAtTwoPaths_PotentialUpdateTargetsItsOwnPath()
    {
        // Arrange: the song is linked at two device paths; only the second one is checked
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        scenario.CreateSongDevice(device, song, "Copy A.mp3");
        scenario.CreateSongDevice(device, song, "Copy B.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "Copy B.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert: the download and the rename act on the checked path, never on the other copy
        result.ShouldNotBeNull();
        result.Records.Single(r => r.Action == SyncRecordAction.UpdateLocal).FilePath.ShouldBe("Copy B.mp3");
        var rename = result.Records.Single(r => r.Action == SyncRecordAction.Rename);
        SyncActionDataSerializer.Deserialize<RenameData>(rename.Data)!.PreviousPath.ShouldBe("Copy B.mp3");
    }

    [Fact]
    public async Task ResolveAsync_SongLinkedAtTwoPaths_ConflictWithPreviousVersionTargetsItsOwnPath()
    {
        // Arrange: the song is linked at two device paths; the second one holds a previous version
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        scenario.AddChecksumHistory(song, ComputeChecksum(clientContent));
        scenario.CreateSongDevice(device, song, "Copy A.mp3");
        scenario.CreateSongDevice(device, song, "Copy B.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "Copy B.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert: the download and the rename act on the checked path, never on the other copy
        result.ShouldNotBeNull();
        result.Records.Single(r => r.Action == SyncRecordAction.UpdateLocal).FilePath.ShouldBe("Copy B.mp3");
        var rename = result.Records.Single(r => r.Action == SyncRecordAction.Rename);
        SyncActionDataSerializer.Deserialize<RenameData>(rename.Data)!.PreviousPath.ShouldBe("Copy B.mp3");
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateChecksumsDiffer_PathChanged_AlsoCreatesRenameRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        var expectedPath = ComputeExpectedPath(song);
        // Device path differs from the template-generated path -> a rename record is produced.
        scenario.CreateSongDevice(device, song, "OldName.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "OldName.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.ShouldContain(r => r.Action == SyncRecordAction.UpdateLocal);
        var renameRecords = result.Records.Where(r => r.Action == SyncRecordAction.Rename).ToList();
        renameRecords.Count.ShouldBe(1);
        renameRecords[0].FilePath.ShouldBe(expectedPath);
    }

    [Theory]
    [InlineData("session/{{ simple_label }}{{ extension }}", "session/")]
    // Sessions started before the template was recorded on them have none
    [InlineData(null, "device/")]
    public async Task ResolveAsync_RenamesWithSessionNamingTemplate_FallingBackToDeviceTemplate(
        string? sessionTemplate, string expectedFolder)
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice(namingTemplate: "device/{{ simple_label }}{{ extension }}");
        var session = scenario.CreateSession(device, isDryRun: true, namingTemplate: sessionTemplate);
        var service = CreateService(scenario);

        var song = scenario.CreateSong("Song", checksum: ComputeChecksum([1, 2, 3, 4, 5]));
        scenario.CreateSongDevice(device, song, "OldName.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "OldName.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(new byte[] { 9, 8, 7, 6, 5 }),
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var rename = result.Records.Single(r => r.Action == SyncRecordAction.Rename);
        rename.FilePath.ShouldStartWith(expectedFolder);
    }

    [Fact]
    public async Task ResolveAsync_DirectionUp_PotentialUpdateChecksumsDiffer_CreatesSkippedRecordInsteadOfUpdateLocal()
    {
        // Arrange: a potential update whose checksum differs, with a device path that would
        // normally also trigger a Rename. In `up` the device never processes server actions.
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, direction: SyncDirection.Up);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        scenario.CreateSongDevice(device, song, "OldName.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "OldName.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Skipped);
        record.FilePath.ShouldBe("OldName.mp3");
        record.SongId.ShouldBe(song.Id);
        scenario.DbContext.DeviceSyncSessionRecords.ShouldNotContain(r =>
            r.Action == SyncRecordAction.UpdateLocal || r.Action == SyncRecordAction.Rename);
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateSongDeviceNotFound_SkipsNoRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        // No SongDevice for this song on this device.

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(content),
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateInvalidBase64_CreatesErrorRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = "!!!not-base64!!!",
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.Error);
        result.Records[0].Reason.ShouldBe("Invalid file content format");
    }

    [Fact]
    public async Task ResolveAsync_MultipleConflicts_ProducesOneRecordPerConflict()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content1 = new byte[] { 1, 2, 3 };
        var content2 = new byte[] { 4, 5, 6 };
        var song1 = scenario.CreateSong("Song1", checksum: ComputeChecksum(content1));
        var song2 = scenario.CreateSong("Song2", checksum: ComputeChecksum(content2));
        scenario.CreateSongDevice(device, song1, "/music/song1.mp3");
        scenario.CreateSongDevice(device, song2, "/music/song2.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song1.mp3",
                SongId = song1.Id,
                FileContentBase64 = Convert.ToBase64String(content1),
                LocalModifiedAt = DateTime.UtcNow,
            },
            new SyncResolveConflictItem
            {
                Path = "/music/song2.mp3",
                SongId = song2.Id,
                FileContentBase64 = Convert.ToBase64String(new byte[] { 99 }),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Count.ShouldBe(2);
        result.Records.ShouldContain(r => r.Action == SyncRecordAction.UpdateTimestamp && r.SongId == song1.Id);
        result.Records.ShouldContain(r => r.Action == SyncRecordAction.Conflict && r.SongId == song2.Id);
    }

    [Fact]
    public async Task ResolveAsync_ConflictChecksumsDiffer_DoesNotMutateLastSyncedModifiedAt()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        var sd = scenario.CreateSongDevice(device, song, "/music/song.mp3",
            lastSyncedModifiedAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        var unchangedSd = await scenario.DbContext.SongDevices.FirstAsync(s => s.Id == sd.Id);
        unchangedSd.LastSyncedModifiedAt.ShouldBe(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ResolveAsync_ConflictWithMatchingChecksum_CreatesUpdateTimestampRecord()
    {
        // Arrange: the client hashes the file itself and sends only the checksum
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                Checksum = ComputeChecksum(content),
                ChecksumAlgorithm = song.ChecksumAlgorithm,
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
        record.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task ResolveAsync_ConflictWithChecksumOfPreviousVersion_CreatesUpdateLocalRecord()
    {
        // Arrange: the sent checksum matches an older version of the song, so the local file is stale
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        scenario.AddChecksumHistory(song, ComputeChecksum(clientContent));
        var devicePath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, devicePath);

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = devicePath,
                SongId = song.Id,
                Checksum = ComputeChecksum(clientContent),
                ChecksumAlgorithm = song.ChecksumAlgorithm,
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateLocal);
        record.FilePath.ShouldBe(devicePath);
    }

    [Fact]
    public async Task ResolveAsync_ConflictWithDifferingChecksum_CreatesConflictRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientChecksum = ComputeChecksum([9, 8, 7, 6, 5]);
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                Checksum = clientChecksum,
                ChecksumAlgorithm = song.ChecksumAlgorithm,
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert: the conflict record carries the checksum exactly as the client sent it
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Conflict);
        var data = SyncActionDataSerializer.Deserialize<ConflictData>(record.Data)!;
        data.LocalChecksum.ShouldBe(clientChecksum);
        data.ServerChecksum.ShouldBe(song.Checksum);
    }

    [Fact]
    public async Task ResolveAsync_ChecksumTakesPrecedenceOverFileContent()
    {
        // Arrange: both fields are sent; the file content would differ, the checksum matches
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                Checksum = ComputeChecksum(content),
                ChecksumAlgorithm = song.ChecksumAlgorithm,
                FileContentBase64 = Convert.ToBase64String(new byte[] { 9, 9, 9 }),
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Single().Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
    }

    [Theory]
    [InlineData("Sha256")]
    [InlineData(null)]
    public async Task ResolveAsync_ConflictWithOtherChecksumAlgorithm_CreatesErrorRecord(string? algorithm)
    {
        // Arrange: a checksum from another algorithm can never be compared with the song's
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                Checksum = ComputeChecksum(content),
                ChecksumAlgorithm = algorithm,
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Error);
        record.Reason.ShouldBe("Unsupported checksum algorithm");
    }

    [Fact]
    public async Task ResolveAsync_ConflictWithoutChecksumOrFileContent_CreatesErrorRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var song = scenario.CreateSong("Song", checksum: ComputeChecksum([1, 2, 3]));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(conflicts:
        [
            new SyncResolveConflictItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                LocalModifiedAt = DateTime.UtcNow,
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Error);
        record.Reason.ShouldBe("Missing checksum or file content");
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateWithMatchingChecksum_CreatesUpdateTimestampRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                Checksum = ComputeChecksum(content),
                ChecksumAlgorithm = song.ChecksumAlgorithm,
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Records.Single().Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateWithDifferingChecksum_CreatesUpdateLocalRecord()
    {
        // Arrange: the device path matches the template-generated path so no rename is produced
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var song = scenario.CreateSong("Song", checksum: ComputeChecksum([1, 2, 3, 4, 5]));
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, expectedPath);

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = expectedPath,
                SongId = song.Id,
                Checksum = ComputeChecksum([9, 8, 7, 6, 5]),
                ChecksumAlgorithm = song.ChecksumAlgorithm,
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.UpdateLocal);
        record.FilePath.ShouldBe(expectedPath);
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateWithOtherChecksumAlgorithm_CreatesErrorRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var content = new byte[] { 1, 2, 3 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(content));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                Checksum = ComputeChecksum(content),
                ChecksumAlgorithm = "Sha256",
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Error);
        record.Reason.ShouldBe("Unsupported checksum algorithm");
    }

    [Fact]
    public async Task ResolveAsync_PotentialUpdateWithoutChecksumOrFileContent_CreatesErrorRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var song = scenario.CreateSong("Song", checksum: ComputeChecksum([1, 2, 3]));
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var input = InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = "/music/song.mp3",
                SongId = song.Id,
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var result = await service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var record = result.Records.Single();
        record.Action.ShouldBe(SyncRecordAction.Error);
        record.Reason.ShouldBe("Missing checksum or file content");
    }

    [Fact]
    public async Task ResolveAsync_CopiesOfASongInSeparateRequests_AreRenamedToDifferentPaths()
    {
        // Arrange: two copies of a song, both needing the server's file; each is sent in its own chunk
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);

        var serverContent = new byte[] { 1, 2, 3, 4, 5 };
        var clientContent = new byte[] { 9, 8, 7, 6, 5 };
        var song = scenario.CreateSong("Song", checksum: ComputeChecksum(serverContent));
        var expectedPath = ComputeExpectedPath(song);
        scenario.CreateSongDevice(device, song, "Copy A.mp3");
        scenario.CreateSongDevice(device, song, "Copy B.mp3");

        SyncResolveConflictsInput InputForCopy(string path) => InputFor(potentialUpdates:
        [
            new SyncResolvePotentialUpdateItem
            {
                Path = path,
                SongId = song.Id,
                FileContentBase64 = Convert.ToBase64String(clientContent),
                LocalModifiedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow.AddHours(-2),
            }
        ]);

        // Act
        var first = await CreateService(scenario).ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, InputForCopy("Copy A.mp3"), CancellationToken.None);
        var second = await CreateService(scenario).ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, InputForCopy("Copy B.mp3"), CancellationToken.None);

        // Assert: the path taken by the first request stays taken
        var firstRename = first!.Records.Single(r => r.Action == SyncRecordAction.Rename);
        var secondRename = second!.Records.Single(r => r.Action == SyncRecordAction.Rename);
        firstRename.FilePath.ShouldBe(expectedPath);
        secondRename.FilePath.ShouldNotBe(expectedPath);
    }

    [Fact]
    public async Task ResolveAsync_SaveFails_LeavesNoRecords()
    {
        // Arrange: two files needing the server's version, each one an UpdateLocal and a Rename
        var saveFailure = new SaveFailure();
        var scenario = new Scenario(saveFailure);
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress);
        var service = CreateService(scenario);

        var first = scenario.CreateSong("First", checksum: ComputeChecksum([1, 2, 3]));
        var second = scenario.CreateSong("Second", checksum: ComputeChecksum([4, 5, 6]));
        scenario.CreateSongDevice(device, first, "Old First.mp3");
        scenario.CreateSongDevice(device, second, "Old Second.mp3");

        SyncResolvePotentialUpdateItem UpdateFor(Song song, string path) => new()
        {
            Path = path,
            SongId = song.Id,
            Checksum = ComputeChecksum([9, 8, 7]),
            ChecksumAlgorithm = song.ChecksumAlgorithm,
            LocalModifiedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow.AddHours(-2),
        };

        var input = InputFor(potentialUpdates: [UpdateFor(first, "Old First.mp3"), UpdateFor(second, "Old Second.mp3")]);

        // Act: the request is cancelled while creating the records of the second file
        saveFailure.FailOnSave = 3;
        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.ResolveAsync(device.Id, session.Id, scenario.AdminUser.Id, input, CancellationToken.None));

        // Assert: the records of the first file are gone too
        var saved = await scenario.DbContext.DeviceSyncSessionRecords.AsNoTracking()
            .Where(r => r.SessionId == session.Id)
            .ToListAsync();
        saved.ShouldBeEmpty();
    }
}
