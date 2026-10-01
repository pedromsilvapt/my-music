using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

/// <summary>
/// Soundalike deduplication of uploads (sessions started with <c>Deduplicate</c>), using the real
/// <see cref="SyncSoundalikeMatcher"/> with fingerprints faked by <see cref="IFpcalcService"/>.
/// </summary>
public class SyncUploadServiceDeduplicateSpecs
{
    private readonly Scenario _scenario = new();
    private readonly IMusicService _musicService = Substitute.For<IMusicService>();
    private readonly ISongFileValidateService _songFileValidate = Substitute.For<ISongFileValidateService>();
    private readonly IFpcalcService _fpcalc = Substitute.For<IFpcalcService>();
    private readonly SyncSoundalikeSessionCache _cache = new();
    private readonly Device _device;

    public SyncUploadServiceDeduplicateSpecs()
    {
        _songFileValidate.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        _musicService.FindUserSongsByChecksum(
                Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Song>());
        _fpcalc.IsAvailable().Returns(true);
        _device = _scenario.CreateDevice();
    }

    private SyncUploadService CreateService(ISyncSoundalikeMatcher? matcher = null) =>
        new(
            _scenario.DbContext,
            _scenario.FileSystem,
            _musicService,
            _songFileValidate,
            new SyncActionsServerFactory(),
            matcher ?? new SyncSoundalikeMatcher(
                _scenario.DbContext,
                _fpcalc,
                new AcousticFingerprintService(_scenario.DbContext, _fpcalc, Substitute.For<ILogger<AcousticFingerprintService>>()),
                _cache,
                Options.Create(new AuditConfig()),
                Substitute.For<ILogger<SyncSoundalikeMatcher>>()),
            Substitute.For<ILogger<SyncUploadService>>());

    /// <summary>Makes fpcalc return <paramref name="fingerprint"/> for files staged with <paramref name="fileName"/>.</summary>
    private void ArrangeUploadFingerprint(string fileName, uint[]? fingerprint) =>
        _fpcalc.FingerprintAsync(Arg.Is<string>(p => p.EndsWith("-" + fileName)), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(fingerprint == null ? null : new FpcalcResult { Fingerprint = fingerprint, Duration = 200 });

    private Song CreateLibrarySong(string title, uint[] fingerprint)
    {
        var song = _scenario.CreateSong(title);
        _fpcalc.FingerprintAsync(song.RepositoryPath, Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new FpcalcResult { Fingerprint = fingerprint, Duration = 200 });
        return song;
    }

    private Task<SyncUploadResult> UploadAsync(
        SyncUploadService service, DeviceSyncSession session, string path,
        bool deduplicate = true, bool isUpdate = false, SongDevice? songDevice = null) =>
        service.UploadAsync(
            deviceId: _device.Id,
            sessionId: session.Id,
            isDryRun: session.IsDryRun,
            path: path,
            // Every path has its own content, so no two uploads share a checksum
            fileStream: new MemoryStream(Encoding.UTF8.GetBytes(path)),
            fileName: Path.GetFileName(path),
            modifiedAt: DateTime.UtcNow,
            createdAt: DateTime.UtcNow,
            isUpdate: isUpdate,
            songDeviceForImport: songDevice,
            repositoryPath: "/data",
            ownerId: _scenario.AdminUser.Id,
            direction: session.Direction,
            deduplicate: deduplicate,
            cancellationToken: CancellationToken.None);

    private static SongModifiedAtData Data(DeviceSyncSessionRecord record) =>
        SyncActionDataSerializer.Deserialize<SongModifiedAtData>(record.Data)!;

    [Fact]
    public async Task Upload_SoundalikeOfLibrarySong_LinksToItAndDownloadsIt()
    {
        var fingerprint = FingerprintSamples.Random(1);
        var song = CreateLibrarySong("Song", fingerprint);
        var session = _scenario.CreateSession(_device, repositoryPath: "/data");
        ArrangeUploadFingerprint("copy.mp3", FingerprintSamples.SoundalikeOf(fingerprint));

        var result = await UploadAsync(CreateService(), session, "/music/copy.mp3");

        // The file is linked to the song it sounds like, and the device replaces it with the song's file
        result.Records.Select(r => r.Action).ShouldBe([SyncRecordAction.Link, SyncRecordAction.UpdateLocal]);
        var link = Data(result.Records[0]);
        link.SongId.ShouldBe(song.Id);
        link.IsSoundalike.ShouldBe(true);
        link.Checksum.ShouldBe(song.Checksum);
        link.LocalChecksum.ShouldNotBe(song.Checksum);
        result.Records[1].SongId.ShouldBe(song.Id);
        Data(result.Records[1]).LocalSourcePath.ShouldBeNull();
        result.EffectiveSongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task Upload_SoundalikeOfLibrarySong_DirectionUp_LinksWithoutDownloading()
    {
        var fingerprint = FingerprintSamples.Random(1);
        CreateLibrarySong("Song", fingerprint);
        var session = _scenario.CreateSession(_device, repositoryPath: "/data", direction: SyncDirection.Up);
        ArrangeUploadFingerprint("copy.mp3", FingerprintSamples.SoundalikeOf(fingerprint));

        var result = await UploadAsync(CreateService(), session, "/music/copy.mp3");

        result.Records.Select(r => r.Action).ShouldBe([SyncRecordAction.Link, SyncRecordAction.Skipped]);
    }

    [Fact]
    public async Task Upload_SoundalikeOfSessionUpload_LinksToItsChecksumAndCopiesIt()
    {
        // The first file is new to the library; the second one sounds like it
        var fingerprint = FingerprintSamples.Random(1);
        var session = _scenario.CreateSession(_device, repositoryPath: "/data");
        ArrangeUploadFingerprint("first.mp3", fingerprint);
        ArrangeUploadFingerprint("second.mp3", FingerprintSamples.SoundalikeOf(fingerprint));
        var service = CreateService();

        var first = await UploadAsync(service, session, "/music/first.mp3");
        var second = await UploadAsync(service, session, "/music/second.mp3");

        // The first file is created on the server
        first.Records.ShouldHaveSingleItem().Action.ShouldBe(SyncRecordAction.CreateRemote);
        var firstChecksum = SyncActionDataSerializer.Deserialize<CreateRemoteData>(first.Records[0].Data)!.Checksum;

        // The second one is linked to the first one's song, and the device copies the first file over it
        second.Records.Select(r => r.Action).ShouldBe([SyncRecordAction.Link, SyncRecordAction.UpdateLocal]);
        var link = Data(second.Records[0]);
        link.SongId.ShouldBeNull();
        link.Checksum.ShouldBe(firstChecksum);
        link.LocalChecksum.ShouldNotBe(firstChecksum);
        link.IsSoundalike.ShouldBe(true);
        Data(second.Records[1]).LocalSourcePath.ShouldBe("/music/first.mp3");
        second.EffectiveSongId.ShouldBeNull();
    }

    [Fact]
    public async Task Upload_SoundalikeOfSessionUpload_DirectionUp_LinksWithoutCopying()
    {
        var fingerprint = FingerprintSamples.Random(1);
        var session = _scenario.CreateSession(_device, repositoryPath: "/data", direction: SyncDirection.Up);
        ArrangeUploadFingerprint("first.mp3", fingerprint);
        ArrangeUploadFingerprint("second.mp3", FingerprintSamples.SoundalikeOf(fingerprint));
        var service = CreateService();

        await UploadAsync(service, session, "/music/first.mp3");
        var second = await UploadAsync(service, session, "/music/second.mp3");

        second.Records.Select(r => r.Action).ShouldBe([SyncRecordAction.Link, SyncRecordAction.Skipped]);
    }

    [Fact]
    public async Task Upload_NoSoundalike_CreatesRemote()
    {
        CreateLibrarySong("Song", FingerprintSamples.Random(1));
        var session = _scenario.CreateSession(_device, repositoryPath: "/data");
        ArrangeUploadFingerprint("new.mp3", FingerprintSamples.Random(2));

        var result = await UploadAsync(CreateService(), session, "/music/new.mp3");

        result.Records.ShouldHaveSingleItem().Action.ShouldBe(SyncRecordAction.CreateRemote);
    }

    [Fact]
    public async Task Upload_FileCannotBeFingerprinted_CreatesRemote()
    {
        CreateLibrarySong("Song", FingerprintSamples.Random(1));
        var session = _scenario.CreateSession(_device, repositoryPath: "/data");
        ArrangeUploadFingerprint("new.mp3", null);

        var result = await UploadAsync(CreateService(), session, "/music/new.mp3");

        result.Records.ShouldHaveSingleItem().Action.ShouldBe(SyncRecordAction.CreateRemote);
    }

    [Fact]
    public async Task Upload_WithoutDeduplicate_DoesNotCheckSoundalikes()
    {
        var matcher = Substitute.For<ISyncSoundalikeMatcher>();
        var session = _scenario.CreateSession(_device, repositoryPath: "/data");

        var result = await UploadAsync(CreateService(matcher), session, "/music/new.mp3", deduplicate: false);

        result.Records.ShouldHaveSingleItem().Action.ShouldBe(SyncRecordAction.CreateRemote);
        await matcher.DidNotReceiveWithAnyArgs().MatchOrRegisterAsync(default, default, default!, default!, default!, default);
    }

    [Fact]
    public async Task Upload_UpdatedFile_IsNotCheckedForSoundalikes()
    {
        var matcher = Substitute.For<ISyncSoundalikeMatcher>();
        var song = _scenario.CreateSong("Song");
        var songDevice = _scenario.CreateSongDevice(_device, song, "/music/song.mp3");
        var session = _scenario.CreateSession(_device, repositoryPath: "/data");

        var result = await UploadAsync(CreateService(matcher), session, "/music/song.mp3", isUpdate: true, songDevice: songDevice);

        result.Records.ShouldHaveSingleItem().Action.ShouldBe(SyncRecordAction.UpdateRemote);
        await matcher.DidNotReceiveWithAnyArgs().MatchOrRegisterAsync(default, default, default!, default!, default!, default);
    }

    [Fact]
    public async Task Upload_InvalidFile_IsNotCheckedNorRemembered()
    {
        // An unimportable file is never created on the server, so later uploads cannot be linked to it
        var matcher = Substitute.For<ISyncSoundalikeMatcher>();
        _songFileValidate.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("Unreadable file");
        var session = _scenario.CreateSession(_device, repositoryPath: "/data");

        var result = await UploadAsync(CreateService(matcher), session, "/music/broken.mp3");

        result.Records.ShouldHaveSingleItem().Action.ShouldBe(SyncRecordAction.Error);
        await matcher.DidNotReceiveWithAnyArgs().MatchOrRegisterAsync(default, default, default!, default!, default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Upload_DryRunAndRealRun_RecordTheSameActionsAndSaveNoUploadFingerprint(bool isDryRun)
    {
        // A library song, a soundalike of it, a new file, and a soundalike of that new file
        var libraryFingerprint = FingerprintSamples.Random(1);
        var newFingerprint = FingerprintSamples.Random(2);
        var song = CreateLibrarySong("Song", libraryFingerprint);
        var session = _scenario.CreateSession(_device, isDryRun: isDryRun, repositoryPath: "/data");
        ArrangeUploadFingerprint("library-copy.mp3", FingerprintSamples.SoundalikeOf(libraryFingerprint));
        ArrangeUploadFingerprint("new.mp3", newFingerprint);
        ArrangeUploadFingerprint("new-copy.mp3", FingerprintSamples.SoundalikeOf(newFingerprint));
        var service = CreateService();

        var records = new List<DeviceSyncSessionRecord>();
        foreach (var path in new[] { "/music/library-copy.mp3", "/music/new.mp3", "/music/new-copy.mp3" })
        {
            records.AddRange((await UploadAsync(service, session, path)).Records);
        }

        // Both modes record the same actions
        records.Select(r => (r.FilePath, r.Action)).ShouldBe([
            ("/music/library-copy.mp3", SyncRecordAction.Link),
            ("/music/library-copy.mp3", SyncRecordAction.UpdateLocal),
            ("/music/new.mp3", SyncRecordAction.CreateRemote),
            ("/music/new-copy.mp3", SyncRecordAction.Link),
            ("/music/new-copy.mp3", SyncRecordAction.UpdateLocal),
        ]);

        // Only the library song's fingerprint is saved: nothing about the uploads is, before the commit
        _scenario.DbContext.SongAcousticFingerprints.ShouldHaveSingleItem().Checksum.ShouldBe(song.Checksum);
    }
}
