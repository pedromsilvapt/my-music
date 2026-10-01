using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncSoundalikeMatcherSpecs
{
    private const long SessionId = 1;
    private const string UploadPath = "/data/.temp/sync-1/upload.mp3";

    private readonly Scenario _scenario = new();
    private readonly IFpcalcService _fpcalc = Substitute.For<IFpcalcService>();
    private readonly SyncSoundalikeSessionCache _cache = new();
    private readonly SyncSoundalikeMatcher _matcher;

    public SyncSoundalikeMatcherSpecs()
    {
        _fpcalc.IsAvailable().Returns(true);
        _matcher = new SyncSoundalikeMatcher(
            _scenario.DbContext,
            _fpcalc,
            new AcousticFingerprintService(_scenario.DbContext, _fpcalc, Substitute.For<ILogger<AcousticFingerprintService>>()),
            _cache,
            Options.Create(new AuditConfig()),
            Substitute.For<ILogger<SyncSoundalikeMatcher>>());
    }

    private void ArrangeFingerprint(string filePath, uint[]? fingerprint) =>
        _fpcalc.FingerprintAsync(filePath, Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(fingerprint == null ? null : new FpcalcResult { Fingerprint = fingerprint, Duration = 200 });

    private Task<SyncSoundalikeMatch?> MatchOrRegisterAsync(
        string checksum = "upload-checksum", string devicePath = "/music/upload.mp3", long sessionId = SessionId) =>
        _matcher.MatchOrRegisterAsync(sessionId, _scenario.AdminUser.Id, UploadPath, checksum, devicePath, CancellationToken.None);

    [Fact]
    public async Task MatchOrRegister_FileCannotBeFingerprinted_ReturnsNullWithoutRegistering()
    {
        ArrangeFingerprint(UploadPath, null);

        (await MatchOrRegisterAsync()).ShouldBeNull();

        _cache.Get(SessionId).Uploads.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MatchOrRegister_SoundalikeOfLibrarySong_MatchesItAndSavesOnlyTheLibraryFingerprint()
    {
        // A library song without a stored fingerprint, and an upload that sounds like it
        var song = _scenario.CreateSong("Song");
        var songFingerprint = FingerprintSamples.Random(1);
        ArrangeFingerprint(song.RepositoryPath, songFingerprint);
        ArrangeFingerprint(UploadPath, FingerprintSamples.SoundalikeOf(songFingerprint));

        var match = await MatchOrRegisterAsync();

        // The upload matches the song, is not remembered as a session upload, and only the song's
        // fingerprint is saved
        match.ShouldNotBeNull();
        match.SongId.ShouldBe(song.Id);
        _cache.Get(SessionId).Uploads.Count.ShouldBe(0);
        _scenario.DbContext.SongAcousticFingerprints.ShouldHaveSingleItem().Checksum.ShouldBe(song.Checksum);
    }

    [Fact]
    public async Task MatchOrRegister_StoredLibraryFingerprint_IsUsedWithoutFingerprintingTheSong()
    {
        var song = _scenario.CreateSong("Song");
        var songFingerprint = FingerprintSamples.Random(1);
        _scenario.DbContext.SongAcousticFingerprints.Add(new SongAcousticFingerprint
        {
            Checksum = song.Checksum,
            ChecksumAlgorithm = song.ChecksumAlgorithm,
            OwnerId = song.OwnerId,
            Fingerprint = FingerprintEncoding.ToBytes(songFingerprint),
            FingerprintLength = AcousticFingerprintService.DefaultFingerprintLength,
            FingerprintAlgorithm = AcousticFingerprintService.DefaultFingerprintAlgorithm,
        });
        await _scenario.DbContext.SaveChangesAsync();
        ArrangeFingerprint(UploadPath, FingerprintSamples.SoundalikeOf(songFingerprint));

        var match = await MatchOrRegisterAsync();

        match!.SongId.ShouldBe(song.Id);
        await _fpcalc.DidNotReceive().FingerprintAsync(song.RepositoryPath, Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MatchOrRegister_DifferentSong_RegistersTheUploadWithoutSavingIt()
    {
        var song = _scenario.CreateSong("Song");
        ArrangeFingerprint(song.RepositoryPath, FingerprintSamples.Random(1));
        ArrangeFingerprint(UploadPath, FingerprintSamples.Random(2));

        (await MatchOrRegisterAsync()).ShouldBeNull();

        _cache.Get(SessionId).UploadPaths.ShouldContainKeyAndValue("upload-checksum", "/music/upload.mp3");
        _scenario.DbContext.SongAcousticFingerprints.ShouldAllBe(f => f.Checksum == song.Checksum);
    }

    [Fact]
    public async Task MatchOrRegister_SoundalikeOfSessionUpload_MatchesItWithoutSavingAnything()
    {
        // An earlier upload of the session, which no library song sounds like
        var fingerprint = FingerprintSamples.Random(1);
        ArrangeFingerprint(UploadPath, fingerprint);
        (await MatchOrRegisterAsync("first-checksum", "/music/first.mp3")).ShouldBeNull();

        // A later upload that sounds like it
        ArrangeFingerprint(UploadPath, FingerprintSamples.SoundalikeOf(fingerprint));
        var match = await MatchOrRegisterAsync("second-checksum", "/music/second.mp3");

        // The later upload matches the earlier one, and nothing about either upload is saved
        match.ShouldNotBeNull();
        match.SongId.ShouldBeNull();
        match.UploadChecksum.ShouldBe("first-checksum");
        match.UploadPath.ShouldBe("/music/first.mp3");
        _scenario.DbContext.SongAcousticFingerprints.ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchOrRegister_UploadOfAnotherSession_IsNotMatched()
    {
        var fingerprint = FingerprintSamples.Random(1);
        ArrangeFingerprint(UploadPath, fingerprint);
        await MatchOrRegisterAsync("first-checksum", "/music/first.mp3", sessionId: SessionId + 1);

        ArrangeFingerprint(UploadPath, FingerprintSamples.SoundalikeOf(fingerprint));
        (await MatchOrRegisterAsync("second-checksum", "/music/second.mp3")).ShouldBeNull();
    }

    [Fact]
    public async Task MatchOrRegister_ConcurrentSoundalikes_OnlyOneIsRegistered()
    {
        // Two soundalikes uploaded at the same time cannot both miss each other
        var fingerprint = FingerprintSamples.Random(1);
        ArrangeFingerprint(UploadPath, fingerprint);

        var matches = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(i => MatchOrRegisterAsync($"checksum-{i}", $"/music/{i}.mp3")));

        matches.Count(m => m == null).ShouldBe(1);
        _cache.Get(SessionId).Uploads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EndSession_ForgetsTheSessionUploads()
    {
        var fingerprint = FingerprintSamples.Random(1);
        ArrangeFingerprint(UploadPath, fingerprint);
        await MatchOrRegisterAsync("first-checksum", "/music/first.mp3");

        _matcher.EndSession(SessionId);

        _cache.Contains(SessionId).ShouldBeFalse();
        ArrangeFingerprint(UploadPath, FingerprintSamples.SoundalikeOf(fingerprint));
        (await MatchOrRegisterAsync("second-checksum", "/music/second.mp3")).ShouldBeNull();
    }

    [Fact]
    public async Task SaveSongFingerprint_SavesTheSongsFingerprint()
    {
        var song = _scenario.CreateSong("Song");
        ArrangeFingerprint(song.RepositoryPath, FingerprintSamples.Random(1));

        await _matcher.SaveSongFingerprintAsync(song.Id, CancellationToken.None);

        _scenario.DbContext.SongAcousticFingerprints.ShouldHaveSingleItem().Checksum.ShouldBe(song.Checksum);
    }

    [Fact]
    public void Cache_EvictsSessionsIdleForLongerThanTheTimeout()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var cache = new SyncSoundalikeSessionCache(time);
        cache.Get(1);

        time.Advance(SyncSoundalikeSessionCache.IdleTimeout + TimeSpan.FromMinutes(1));
        cache.Get(2);

        cache.Contains(1).ShouldBeFalse();
        cache.Contains(2).ShouldBeTrue();
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
