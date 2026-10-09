namespace MyMusic.CLI.Services.Sync;

/// <summary>
/// Compares file dates the way the server's sync does. Mirrors <c>SyncComparisonHelper</c> in
/// MyMusic.Common/Services/Sync/SyncComparisonHelper.cs (the CLI does not reference MyMusic.Common): both are
/// pinned by the same test vectors.
/// </summary>
public static class SyncDateComparison
{
    /// <summary>
    /// Whether <paramref name="current"/> is later than <paramref name="reference"/>, ignoring the last digit
    /// of the ticks, which the database does not keep.
    /// </summary>
    public static bool IsNewerThan(DateTime current, DateTime reference)
    {
        return current.Ticks / 10 > reference.Ticks / 10;
    }
}
