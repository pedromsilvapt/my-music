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
    private readonly ISyncSoundalikeMatcher _soundalikeMatcher = Substitute.For<ISyncSoundalikeMatcher>();
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
            _soundalikeMatcher,
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.CreateRemote);
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Link);
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.CreateRemote);
        record.Data.ShouldNotBeNull();
        var data = record.Data.Value;
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
            direction: session.Direction,
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.CreateRemote);
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
        song.Checksum = checksum;
        scenario.DbContext.SaveChanges();

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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Link);
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Link);
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Error);
        record.FilePath.ShouldBe("/music/song.mp3");
        record.Reason.ShouldBe("Cannot read song metadata: corrupt");
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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Error);
        record.SongId.ShouldBe(song.Id);
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
            direction: session.Direction,
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
        song.Checksum = checksum;
        scenario.DbContext.SaveChanges();

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
            direction: session.Direction,
            cancellationToken: CancellationToken.None);

        // A link never imports the file, so its contents don't need to be readable
        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Link);
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
        ArrangeLibraryChecksum(UploadContent, duplicateSong, scenario);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = await UploadUpdateAsync(service, scenario, session, songDevice, isDryRun);

        // Assert
        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Link);
        record.SongId.ShouldBe(duplicateSong.Id);
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
        ArrangeLibraryChecksum(UploadContent, song, scenario);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = await UploadUpdateAsync(service, scenario, session, songDevice);

        // Assert
        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
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
        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Link);
        record.SongId.ShouldBeNull();
        result.EffectiveSongId.ShouldBeNull();
        SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data)!.Checksum.ShouldBe(UploadChecksum);
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
        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Link);
        record.SongId.ShouldBe(otherSong.Id);
    }

    #endregion

    #region File matching a previous version of a song

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploadAsync_UpdateMatchingPreviousVersionOfOwnSong_CreatesUpdateLocalRecord(bool isDryRun)
    {
        // Arrange: the uploaded content is an older version of the song the file is linked to
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: isDryRun, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        ArrangePreviousVersionChecksum(UploadContent, song);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = await UploadUpdateAsync(service, scenario, session, songDevice, isDryRun);

        // Assert: the server version wins, so the device downloads it; nothing is imported
        result.Records.Select(r => (r.Action, r.SongId, r.FilePath))
            .ShouldBe([(SyncRecordAction.UpdateLocal, song.Id, "/music/song.mp3")]);
        result.EffectiveSongId.ShouldBe(song.Id);
        await _songFileValidate.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploadAsync_NewFileMatchingPreviousVersion_CreatesLinkAndUpdateLocalRecords(bool isDryRun)
    {
        // Arrange: a file the device never synced holds an older version of a library song
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, isDryRun: isDryRun, repositoryPath: "/data");
        ArrangePreviousVersionChecksum(UploadContent, song);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = await UploadNewAsync(service, scenario, session, "/music/copy.mp3");

        // Assert: the file is linked to the song, then replaced by its current version
        result.Records.Select(r => (r.Action, r.SongId, r.FilePath)).ShouldBe([
            (SyncRecordAction.Link, song.Id, "/music/copy.mp3"),
            (SyncRecordAction.UpdateLocal, song.Id, "/music/copy.mp3"),
        ]);
        SyncActionDataSerializer.Deserialize<SongModifiedAtData>(result.Records[0].Data)!.IsPreviousVersion.ShouldBe(true);
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task UploadAsync_UpdateMatchingPreviousVersionOfAnotherSong_CreatesLinkAndUpdateLocalRecords()
    {
        // Arrange: the updated file now holds an older version of a different song
        var scenario = new Scenario();
        var linkedSong = scenario.CreateSong("Linked");
        var otherSong = scenario.CreateSong("Other");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, linkedSong, "/music/song.mp3");
        ArrangePreviousVersionChecksum(UploadContent, otherSong);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = await UploadUpdateAsync(service, scenario, session, songDevice);

        // Assert: the path moves to the other song, and the device downloads its current version
        result.Records.Select(r => (r.Action, r.SongId)).ShouldBe([
            (SyncRecordAction.Link, otherSong.Id),
            (SyncRecordAction.UpdateLocal, otherSong.Id),
        ]);
        SyncActionDataSerializer.Deserialize<SongModifiedAtData>(result.Records[0].Data)!.IsPreviousVersion.ShouldBe(true);
        result.EffectiveSongId.ShouldBe(otherSong.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploadAsync_PreviousVersion_DirectionUp_SkipsInsteadOfUpdateLocal(bool isUpdate)
    {
        // Arrange: pushing only, so the device must not be changed
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data", direction: SyncDirection.Up);
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        ArrangePreviousVersionChecksum(UploadContent, song);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        var result = isUpdate
            ? await UploadUpdateAsync(service, scenario, session, songDevice)
            : await UploadNewAsync(service, scenario, session, "/music/copy.mp3");

        // Assert: the download is recorded as Skipped (a new file is still linked to the song)
        var expected = isUpdate
            ? new[] { SyncRecordAction.Skipped }
            : [SyncRecordAction.Link, SyncRecordAction.Skipped];
        result.Records.Select(r => r.Action).ShouldBe(expected);
    }

    [Fact]
    public async Task UploadAsync_PreviousVersion_Live_DeletesStagedFile()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        ArrangePreviousVersionChecksum(UploadContent, song);
        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        // Act
        await UploadUpdateAsync(service, scenario, session, songDevice);

        // Assert: the file is never imported, so it is not kept for the commit
        scenario.FileSystem.Directory.GetFiles($"/data/.temp/sync-{session.Id}").ShouldBeEmpty();
    }

    #endregion

    private static readonly byte[] UploadContent = [9, 8, 7, 6, 5];

    private static readonly string UploadChecksum = ChecksumService.ComputeChecksumFromBytes(UploadContent, "XxHash128");

    /// <summary>Makes <paramref name="content"/> the current file of <paramref name="song"/> in the library lookup.</summary>
    private void ArrangeLibraryChecksum(byte[] content, Song song, Scenario scenario)
    {
        song.Checksum = ChecksumService.ComputeChecksumFromBytes(content, "XxHash128");
        scenario.DbContext.SaveChanges();
        ArrangePreviousVersionChecksum(content, song);
    }

    /// <summary>
    /// Makes the library lookup return <paramref name="song"/> for <paramref name="content"/>, without it being the
    /// song's current checksum: the content is a previous version of the song's file.
    /// </summary>
    private void ArrangePreviousVersionChecksum(byte[] content, Song song)
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
        UploadUpdateAsync(service, scenario, session, songDevice, isDryRun, session.Direction);

    private static Task<SyncUploadResult> UploadUpdateAsync(
        SyncUploadService service, Scenario scenario, DeviceSyncSession session, SongDevice songDevice, bool isDryRun, SyncDirection direction) =>
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
            direction: direction,
            cancellationToken: CancellationToken.None);

    private static Task<SyncUploadResult> UploadNewAsync(
        SyncUploadService service, Scenario scenario, DeviceSyncSession session, string path) =>
        UploadNewAsync(service, scenario, session, path, session.IsDryRun, session.Direction);

    private static Task<SyncUploadResult> UploadNewAsync(
        SyncUploadService service, Scenario scenario, DeviceSyncSession session, string path, bool isDryRun, SyncDirection direction) =>
        service.UploadAsync(
            deviceId: session.DeviceId,
            sessionId: session.Id,
            isDryRun: isDryRun,
            path: path,
            fileStream: new MemoryStream(UploadContent),
            fileName: Path.GetFileName(path),
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: false,
            songDeviceForImport: null,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            direction: direction,
            cancellationToken: CancellationToken.None);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadAsync_ResolvingConflict_RecordsPointToTheConflict(bool isDryRun)
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data", isDryRun: isDryRun);
        var song = scenario.CreateSong("Existing Song");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        var conflict = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.Conflict, songId: song.Id);

        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: isDryRun,
            path: "/music/song.mp3",
            fileStream: new MemoryStream([1, 2, 3, 4, 5]),
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            direction: session.Direction,
            resolvesConflictRecordId: conflict.Id,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.UpdateRemote);
        record.ResolvesConflictRecordId.ShouldBe(conflict.Id);
        result.Records.CountUnresolvedConflicts().ShouldBe(-1);
    }

    [Fact]
    public async Task UploadAsync_ResolvingConflict_UnimportableFile_LeavesConflictUnresolved()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var song = scenario.CreateSong("Existing Song");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        var conflict = scenario.AddRecord(session.Id, "/music/song.mp3", SyncRecordAction.Conflict, songId: song.Id);
        _songFileValidate.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("Invalid file");

        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: new MemoryStream([1, 2, 3, 4, 5]),
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            direction: session.Direction,
            resolvesConflictRecordId: conflict.Id,
            cancellationToken: CancellationToken.None);

        var record = result.Records.ShouldHaveSingleItem();
        record.Action.ShouldBe(SyncRecordAction.Error);
        record.ResolvesConflictRecordId.ShouldBeNull();
    }

    [Fact]
    public async Task UploadAsync_ResolvingRecordThatIsNotAConflictAtThePath_DoesNotPointToIt()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        var song = scenario.CreateSong("Existing Song");
        var songDevice = scenario.CreateSongDevice(device, song, "/music/song.mp3");
        var otherConflict = scenario.AddRecord(session.Id, "/music/other.mp3", SyncRecordAction.Conflict, songId: song.Id);

        var service = CreateService(scenario.DbContext, scenario.FileSystem);

        var result = await service.UploadAsync(
            deviceId: device.Id,
            sessionId: session.Id,
            isDryRun: false,
            path: "/music/song.mp3",
            fileStream: new MemoryStream([1, 2, 3, 4, 5]),
            fileName: "song.mp3",
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: true,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: scenario.AdminUser.Id,
            direction: session.Direction,
            resolvesConflictRecordId: otherConflict.Id,
            cancellationToken: CancellationToken.None);

        result.Records.ShouldHaveSingleItem().ResolvesConflictRecordId.ShouldBeNull();
    }
}
