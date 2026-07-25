using System.IO.Hashing;
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

public class SyncResolveConflictsServiceSpecs
{
    private const string NamingTemplate = "{{ simple_label }}{{ extension }}";

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
}
