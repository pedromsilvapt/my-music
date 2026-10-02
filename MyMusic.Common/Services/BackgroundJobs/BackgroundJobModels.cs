using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Services.BackgroundJobs;

/// <summary>
/// Work item counters of a background job. A <c>null</c> counter means the job does not keep track of it.
/// </summary>
public record BackgroundJobCounters(int? Queued, int? Processed, int? Failed)
{
    /// <summary>
    /// Counters of a job that keeps no record of its work.
    /// </summary>
    public static readonly BackgroundJobCounters NotTracked = new(null, null, null);
}

/// <summary>
/// A failed work item of a background job, with the information needed to debug it.
/// </summary>
/// <param name="Id">Identifier of the failed work item within its job.</param>
/// <param name="Title">Short human-readable description of the work item.</param>
/// <param name="Message">The error message recorded for the failure.</param>
/// <param name="OccurredAt">When the failure was recorded, if known.</param>
/// <param name="Details">Additional fields of the work item, labelled by their technical name.</param>
public record BackgroundJobFailure(
    string Id,
    string Title,
    string? Message,
    DateTime? OccurredAt,
    IReadOnlyList<BackgroundJobFailureDetail> Details);

/// <summary>
/// A labelled field of a failed work item.
/// </summary>
public record BackgroundJobFailureDetail(string Label, string? Value);

/// <summary>
/// One page of a background job's failures.
/// </summary>
/// <param name="Total">The number of failures across all pages.</param>
public record BackgroundJobFailurePage(int Total, List<BackgroundJobFailure> Items)
{
    /// <summary>
    /// The failures of a job that keeps no record of them.
    /// </summary>
    public static BackgroundJobFailurePage Empty => new(0, []);

    /// <summary>
    /// Counts <paramref name="query"/>, then loads the requested page of it (which must already be ordered) and maps
    /// each loaded item to a failure.
    /// </summary>
    public static async Task<BackgroundJobFailurePage> FromQueryAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        Func<T, BackgroundJobFailure> map,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new BackgroundJobFailurePage(total, items.Select(map).ToList());
    }
}
