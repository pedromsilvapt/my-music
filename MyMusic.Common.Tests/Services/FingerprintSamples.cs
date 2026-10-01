namespace MyMusic.Common.Tests.Services;

/// <summary>
/// Synthetic acoustic fingerprints: random ones, which never sound alike, and soundalikes of them,
/// which differ only in their lowest bit (a score of about 0.97, above the default match threshold).
/// </summary>
internal static class FingerprintSamples
{
    public const int Length = 120;

    public static uint[] Random(int seed)
    {
        var random = new Random(seed);
        var fingerprint = new uint[Length];
        for (var i = 0; i < fingerprint.Length; i++)
        {
            fingerprint[i] = (uint)random.NextInt64(0, uint.MaxValue);
        }
        return fingerprint;
    }

    public static uint[] SoundalikeOf(uint[] fingerprint) =>
        fingerprint.Select(v => v ^ 1u).ToArray();
}
