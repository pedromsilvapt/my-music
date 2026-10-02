namespace MyMusic.Common.Services.BackgroundJobs;

/// <summary>
/// A registered background job and its counters for a user.
/// </summary>
public record BackgroundJobSummary(string Key, BackgroundJobCounters Counters);

/// <summary>
/// Lists every registered background job with its counters for a user.
/// </summary>
public interface IBackgroundJobListService
{
    /// <summary>
    /// Returns the counters of each registered job for the user, in registration order.
    /// </summary>
    Task<List<BackgroundJobSummary>> ListAsync(long userId, CancellationToken cancellationToken);
}

public class BackgroundJobListService(MusicDbContext db, IEnumerable<IQueuedBackgroundJob> jobs)
    : IBackgroundJobListService
{
    public async Task<List<BackgroundJobSummary>> ListAsync(long userId, CancellationToken cancellationToken)
    {
        var summaries = new List<BackgroundJobSummary>();

        // Sequential: the jobs share the same DbContext, which does not support concurrent queries
        foreach (var job in jobs)
        {
            var counters = await job.GetCountersAsync(db, userId, cancellationToken);
            summaries.Add(new BackgroundJobSummary(job.Key, counters));
        }

        return summaries;
    }
}
