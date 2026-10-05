using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SoundalikeMatchServiceSpecs
{
    private readonly Scenario _scenario = new();
    private readonly IFpcalcService _fpcalc = Substitute.For<IFpcalcService>();
    private readonly SoundalikeMatchService _service;

    public SoundalikeMatchServiceSpecs()
    {
        _fpcalc.IsAvailable().Returns(true);
        _service = new SoundalikeMatchService(
            new AcousticFingerprintService(_scenario.DbContext, _fpcalc, Substitute.For<ILogger<AcousticFingerprintService>>()));
    }

    private Song CreateSong(string title, uint[]? fingerprint, long? ownerId = null)
    {
        var song = _scenario.CreateSong(title, ownerId: ownerId, checksum: $"checksum-{title}");
        _fpcalc.FingerprintAsync(song.RepositoryPath, Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(fingerprint == null ? null : new FpcalcResult { Fingerprint = fingerprint, Duration = 200 });
        return song;
    }

    private Task<SoundalikeMatchResult> MatchAsync(params Song[] songs) =>
        _service.MatchAsync(_scenario.DbContext, _scenario.AdminUser.Id, songs.Select(s => s.Id).ToList());

    [Fact]
    public async Task Match_Soundalikes_ReturnsTheirScoreAndTheSongsInTheAskedOrder()
    {
        var fingerprint = FingerprintSamples.Random(1);
        var a = CreateSong("A", fingerprint);
        var b = CreateSong("B", FingerprintSamples.SoundalikeOf(fingerprint));

        var match = await MatchAsync(b, a);

        match.Songs.Select(s => s.Id).ShouldBe([b.Id, a.Id]);
        match.MatchScore.ShouldNotBeNull();
        match.MatchScore.Value.ShouldBeGreaterThan(0.9);
    }

    [Fact]
    public async Task Match_SongsThatDoNotSoundAlike_StillReturnsTheirLowScore()
    {
        var a = CreateSong("A", FingerprintSamples.Random(1));
        var b = CreateSong("B", FingerprintSamples.Random(2));

        var match = await MatchAsync(a, b);

        // Well below the audit's match threshold, and reported anyway
        match.MatchScore.ShouldNotBeNull();
        match.MatchScore.Value.ShouldBeLessThan(new AuditConfig().SoundalikeMatchThreshold);
    }

    [Fact]
    public async Task Match_SeveralSongs_ReturnsTheLowestPairwiseScore()
    {
        var fingerprint = FingerprintSamples.Random(1);
        var a = CreateSong("A", fingerprint);
        var b = CreateSong("B", FingerprintSamples.SoundalikeOf(fingerprint));
        var c = CreateSong("C", FingerprintSamples.Random(2));

        var alike = await MatchAsync(a, b);
        var all = await MatchAsync(a, b, c);

        all.MatchScore.ShouldNotBeNull();
        all.MatchScore.Value.ShouldBeLessThan(alike.MatchScore!.Value);
    }

    [Fact]
    public async Task Match_SongThatCannotBeFingerprinted_ReturnsNoScore()
    {
        var a = CreateSong("A", FingerprintSamples.Random(1));
        var b = CreateSong("B", fingerprint: null);

        var match = await MatchAsync(a, b);

        match.Songs.Count.ShouldBe(2);
        match.MatchScore.ShouldBeNull();
    }

    [Fact]
    public async Task Match_FpcalcNotAvailable_ReturnsNoScore()
    {
        _fpcalc.IsAvailable().Returns(false);
        var a = CreateSong("A", FingerprintSamples.Random(1));
        var b = CreateSong("B", FingerprintSamples.Random(2));

        var match = await MatchAsync(a, b);

        match.Songs.Count.ShouldBe(2);
        match.MatchScore.ShouldBeNull();
    }

    [Fact]
    public async Task Match_SongOfAnotherUser_Throws()
    {
        var other = _scenario.CreateUser("Other", "other");
        var a = CreateSong("A", FingerprintSamples.Random(1));
        var b = CreateSong("B", FingerprintSamples.Random(2), ownerId: other.Id);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => MatchAsync(a, b));
    }
}
