using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncUploadServiceSpecs
{
    private readonly IMusicService _musicService = Substitute.For<IMusicService>();
    private readonly ISyncActionsServerFactory _syncActionsServerFactory = new SyncActionsServerFactory();
    private readonly ISongFileValidateService _songFileValidate = Substitute.For<ISongFileValidateService>();
    private readonly ILogger<SyncUploadService> _logger = Substitute.For<ILogger<SyncUploadService>>();

    public SyncUploadServiceSpecs()
    {
        _songFileValidate.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Song>());
    }

    private SyncUploadService CreateService(MusicDbContext db, IFileSystem? fileSystem = null)
    {
        return new SyncUploadService(
            db,
            fileSystem ?? new MockFileSystem(),
            _musicService,
            _songFileValidate,
            _syncActionsServerFactory,
            _logger);
    }


    [Fact]
    public async Task UploadAsync_NewFile_NoDuplicate_CreatesCreateRemoteRecord()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var fileStream = new MemoryStream(fileContent);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.CreateRemote);
        result.EffectiveSongId.ShouldBeNull();
    }

    [Fact]
    public async Task UploadAsync_ExistingDevice_CreatesUpdateRemoteRecord()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var fileStream = new MemoryStream(fileContent);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_DuplicateWithSongIdInLibrary_CreatesLinkRecord()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var checksum = ChecksumService.ComputeChecksumFromBytes(content, "XxHash128");
        song.Checksum = checksum;
        scenario.DbContext.SaveChanges();

        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");

        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var checksums = call.ArgAt<List<string>>(2);
                if (checksums.Contains(checksum))
                    return new Dictionary<string, Song> { { checksum, song } };
                return new Dictionary<string, Song>();
            });

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(content);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_DryRun_CreatesSameRecordTypeAsLive()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: true, repositoryPath: "/data");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: true,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.CreateRemote);
        result.Record.Data.ShouldNotBeNull();
        var data = result.Record.Data.Value;
        data.TryGetProperty("tempFilePath", out var tempProp).ShouldBeTrue();
        tempProp.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task UploadAsync_DryRun_LeavesNoStagedFileInSessionDirectory()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: true, repositoryPath: "/data");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: true,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        // Dry runs stage into the same session directory, but never keep the file past the request
        mockFs.Directory.GetFiles($"/data/.temp/sync-{session.Id}").ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadAsync_Live_CreateRemote_KeepsStagedFileForCommit()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.CreateRemote);
        mockFs.Directory.GetFiles($"/data/.temp/sync-{session.Id}").Length.ShouldBe(1);
    }

    [Fact]
    public async Task UploadAsync_Live_Link_DeletesStagedFile()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var song = scenario.CreateSong("Song");

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var checksum = ChecksumService.ComputeChecksumFromBytes(content, "XxHash128");

        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");

        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Song> { { checksum, song } });

        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: new MemoryStream(content),
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        mockFs.Directory.GetFiles($"/data/.temp/sync-{session.Id}").ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadAsync_WithDuplicateInSession_LinksToExistingCreateRemoteSongId()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");

        var firstStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var checksum = ChecksumService.ComputeChecksumFromBytes(firstStream.ToArray(), "XxHash128");
        song.Checksum = checksum;
        scenario.DbContext.SaveChanges();

        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");

        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var checksums = call.ArgAt<List<string>>(2);
                if (checksums.Contains(checksum))
                    return new Dictionary<string, Song> { { checksum, song } };
                return new Dictionary<string, Song>();
            });

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_UpdateWithoutDuplicate_CreatesUpdateRemoteRecord()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: fileStream,
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        result.EffectiveSongId.ShouldBe(songDevice.SongId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploadAsync_UnimportableNewFile_CreatesErrorRecordInsteadOfCreateRemote(bool isDryRun)
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: isDryRun, repositoryPath: "/data");

        _songFileValidate.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Cannot read song metadata: corrupt");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: isDryRun,
            path: "/music/song.mp3",
            fileStream: new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.Error);
        result.Record.FilePath.ShouldBe("/music/song.mp3");
        result.Record.Reason.ShouldBe("Cannot read song metadata: corrupt");
        result.EffectiveSongId.ShouldBeNull();

        var records = scenario.DbContext.DeviceSyncSessionRecords.Where(r => r.SessionId == session.Id).ToList();
        records.Select(r => r.Action).ShouldBe([SyncRecordAction.Error]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploadAsync_UnimportableUpdatedFile_CreatesErrorRecordInsteadOfUpdateRemote(bool isDryRun)
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: isDryRun, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");

        _songFileValidate.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Cannot read song metadata: corrupt");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: isDryRun,
            path: "/music/song.mp3",
            fileStream: new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        result.Record.Action.ShouldBe(SyncRecordAction.Error);
        result.Record.SongId.ShouldBe(song.Id);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_UnimportableFile_Live_DeletesStagedFile()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");

        _songFileValidate.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Cannot read song metadata: corrupt");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        mockFs.Directory.GetFiles($"/data/.temp/sync-{session.Id}").ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadAsync_DuplicateChecksum_LinksWithoutValidatingFile()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var checksum = ChecksumService.ComputeChecksumFromBytes(content, "XxHash128");

        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");

        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Song> { { checksum, song } });
        _songFileValidate.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Cannot read song metadata: corrupt");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: new MemoryStream(content),
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

        // A link never imports the file, so its contents don't need to be readable
        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        await _songFileValidate.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #region Updated file duplicating another song

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploadAsync_UpdateDuplicatingAnotherLibrarySong_CreatesLinkToThatSong(bool isDryRun)
    {
        // Arrange
        var scenario = new Scenario();
        var updatedSong = scenario.CreateSong("Updated");
        var duplicateSong = scenario.CreateSong("Duplicate");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: isDryRun, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, updatedSong, "/music/song.mp3");
        ArrangeLibraryChecksum(UploadContent, duplicateSong);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = await UploadUpdateAsync(service, scenario, session, songDevice, isDryRun);

        // Assert
        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        result.Record.SongId.ShouldBe(duplicateSong.Id);
        result.EffectiveSongId.ShouldBe(duplicateSong.Id);
        await _songFileValidate.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_UpdateWithUnchangedContent_CreatesUpdateRemoteRecord()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        ArrangeLibraryChecksum(UploadContent, song);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = await UploadUpdateAsync(service, scenario, session, songDevice);

        // Assert
        result.Record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_UpdateDuplicatingPendingCreateRemote_CreatesChecksumOnlyLink()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        scenario.AddRecord(session.Id, "/music/new.mp3", SyncRecordAction.CreateRemote,
            data: JsonSerializer.SerializeToElement(new { checksum = UploadChecksum, algorithm = "XxHash128", modifiedAt = DateTime.UtcNow.ToString("O") }));
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = await UploadUpdateAsync(service, scenario, session, songDevice);

        // Assert
        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        result.Record.SongId.ShouldBeNull();
        result.EffectiveSongId.ShouldBeNull();
        SyncActionDataSerializer.Deserialize<SongModifiedAtData>(result.Record.Data)!.Checksum.ShouldBe(UploadChecksum);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploadAsync_DuplicatingPendingUpdateRemote_CreatesLinkToUpdatedSong(bool isUpdate)
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var otherSong = scenario.CreateSong("Other");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        scenario.AddRecord(session.Id, "/music/other.mp3", SyncRecordAction.UpdateRemote, songId: otherSong.Id,
            data: JsonSerializer.SerializeToElement(new { songId = otherSong.Id, checksum = UploadChecksum, algorithm = "XxHash128", modifiedAt = DateTime.UtcNow.ToString("O") }));
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = isUpdate
            ? await UploadUpdateAsync(service, scenario, session, songDevice)
            : await UploadNewAsync(service, scenario, session, "/music/copy.mp3");

        // Assert
        result.Record.Action.ShouldBe(SyncRecordAction.Link);
        result.Record.SongId.ShouldBe(otherSong.Id);
    }

    #endregion

    private static readonly byte[] UploadContent = [9, 8, 7, 6, 5];

    private static readonly string UploadChecksum = ChecksumService.ComputeChecksumFromBytes(UploadContent, "XxHash128");

    private void ArrangeLibraryChecksum(byte[] content, Song song)
    {
        var checksum = ChecksumService.ComputeChecksumFromBytes(content, "XxHash128");
        _musicService.FindUserSongsByChecksum(
            Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<List<string>>(2).Contains(checksum)
                ? new Dictionary<string, Song> { { checksum, song } }
                : new Dictionary<string, Song>());
    }

    private static Task<SyncUploadResult> UploadUpdateAsync(
        SyncUploadService service, Scenario scenario, DeviceSyncSession session, SongDevice songDevice, bool isDryRun = false) =>
        service.UploadAsync(
            deviceId: songDevice.DeviceId,
            sessionId: session.Id,
            isDryRun: isDryRun,
            path: songDevice.DevicePath,
            fileStream: new MemoryStream(UploadContent),
            fileName: Path.GetFileName(songDevice.DevicePath),
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);

    private static Task<SyncUploadResult> UploadNewAsync(
        SyncUploadService service, Scenario scenario, DeviceSyncSession session, string path) =>
        service.UploadAsync(
            deviceId: session.DeviceId,
            sessionId: session.Id,
            isDryRun: false,
            path: path,
            fileStream: new MemoryStream(UploadContent),
            fileName: Path.GetFileName(path),
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            cancellationToken: CancellationToken.None);
}
