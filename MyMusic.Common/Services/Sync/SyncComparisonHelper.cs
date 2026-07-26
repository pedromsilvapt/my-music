namespace MyMusic.Common.Services.Sync;

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