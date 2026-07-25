namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Shared timestamp comparison helpers used by the sync workflow.
/// </summary>
public interface ISyncComparisonHelper
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="current"/> is strictly newer than
    /// <paramref name="reference"/>. The dates that are saved in the database by EF Core seem
    /// to lose the precision for the last digit (always 0 when read back), so the comparison
    /// is performed without it on both values.
    /// </summary>
    bool IsNewerThan(DateTime current, DateTime reference);
}

/// <summary>
/// Default implementation of <see cref="ISyncComparisonHelper"/>.
/// </summary>
public class SyncComparisonHelper : ISyncComparisonHelper
{
    /// <inheritdoc />
    public bool IsNewerThan(DateTime current, DateTime reference)
    {
        return current.Ticks / 10 > reference.Ticks / 10;
    }
}