using MyMusic.Common.Services;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class FingerprintIndexSpecs
{
    private const double LookupThreshold = 0.25;
    private const double MatchThreshold = 0.90;

    [Fact]
    public void FindBestMatch_EmptyIndex_ReturnsNull()
    {
        var index = new FingerprintIndex<long>();

        index.FindBestMatch(FingerprintSamples.Random(1), LookupThreshold, MatchThreshold).ShouldBeNull();
    }

    [Fact]
    public void FindBestMatch_Soundalike_ReturnsItsKeyAndScore()
    {
        var index = new FingerprintIndex<long>();
        var fingerprint = FingerprintSamples.Random(1);
        index.Add(1, fingerprint);
        index.Add(2, FingerprintSamples.Random(2));

        var match = index.FindBestMatch(FingerprintSamples.SoundalikeOf(fingerprint), LookupThreshold, MatchThreshold);

        match.ShouldNotBeNull();
        match.Value.Key.ShouldBe(1);
        match.Value.Score.ShouldBeGreaterThanOrEqualTo(MatchThreshold);
    }

    [Fact]
    public void FindBestMatch_DifferentSong_ReturnsNull()
    {
        var index = new FingerprintIndex<long>();
        index.Add(1, FingerprintSamples.Random(1));

        index.FindBestMatch(FingerprintSamples.Random(2), LookupThreshold, MatchThreshold).ShouldBeNull();
    }

    [Fact]
    public void FindBestMatch_SeveralSoundalikes_ReturnsTheClosest()
    {
        var index = new FingerprintIndex<string>();
        var fingerprint = FingerprintSamples.Random(1);
        index.Add("close", FingerprintSamples.SoundalikeOf(fingerprint));
        index.Add("exact", fingerprint);

        var match = index.FindBestMatch(fingerprint, LookupThreshold, MatchThreshold);

        match.ShouldNotBeNull();
        match.Value.Key.ShouldBe("exact");
        match.Value.Score.ShouldBe(1.0);
    }

    [Fact]
    public void Add_ExistingKey_KeepsTheFirstFingerprint()
    {
        var index = new FingerprintIndex<string>();
        var fingerprint = FingerprintSamples.Random(1);
        index.Add("a", fingerprint);
        index.Add("a", FingerprintSamples.Random(2));

        index.Count.ShouldBe(1);
        index.Fingerprints["a"].ShouldBe(fingerprint);
    }

    [Fact]
    public void FingerprintEncoding_RoundTrips()
    {
        var fingerprint = FingerprintSamples.Random(1);

        FingerprintEncoding.FromBytes(FingerprintEncoding.ToBytes(fingerprint)).ShouldBe(fingerprint);
    }
}
