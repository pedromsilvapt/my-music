namespace MyMusic.Common.Services.BackgroundJobs;

/// <summary>
/// Lists the failures of a registered background job for a user.
/// </summary>
public interface IBackgroundJobFailureListService
{
    /// <summary>
    /// Returns one page of the user's failures of the job identified by <paramref name="jobKey"/>, most recent first.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No registered job has the given key.</exception>
    Task<BackgroundJobFailurePage> ListAsync(
        string jobKey,
        long userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public class BackgroundJobFailureListService(MusicDbContext db, IEnumerable<IQueuedBackgroundJob> jobs)
    : IBackgroundJobFailureListService
{
    public async Task<BackgroundJobFailurePage> ListAsync(
        string jobKey,
        long userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var job = jobs.FirstOrDefault(j => j.Key == jobKey)
                  ?? throw new KeyNotFoundException($"No background job is registered with the key '{jobKey}'");

        return await job.GetFailuresAsync(db, userId, page, pageSize, cancellationToken);
    }
}
