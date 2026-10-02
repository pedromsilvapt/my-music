namespace MyMusic.Common.Services.BackgroundJobs;

/// <summary>
/// A background job that reports, from the data it already stores, how much work it has queued, processed and
/// failed for a user. Counters a job does not keep are reported as <c>null</c>.
/// </summary>
public interface IQueuedBackgroundJob
{
    /// <summary>
    /// Stable kebab-case identifier of the job, used in routes and by the client to translate its name.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Counts the queued, processed and failed work items of the job that belong to the user.
    /// </summary>
    Task<BackgroundJobCounters> GetCountersAsync(MusicDbContext db, long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists one page of the user's failed work items, most recent first. Empty for jobs that do not keep failures.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    Task<BackgroundJobFailurePage> GetFailuresAsync(
        MusicDbContext db,
        long userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
