using MyMusic.Common.Services.BackgroundJobs;

namespace MyMusic.Server.DTO.BackgroundJobs;

public record ListBackgroundJobsResponse
{
    public required List<ListBackgroundJobsItem> Jobs { get; set; }
}

/// <summary>
/// A background job and its counters. A <c>null</c> counter means the job does not keep track of it.
/// </summary>
public record ListBackgroundJobsItem
{
    public required string Key { get; set; }
    public required int? Queued { get; set; }
    public required int? Processed { get; set; }
    public required int? Failed { get; set; }
}

public static class ListBackgroundJobsResponseMapping
{
    public static ListBackgroundJobsResponse ToResponse(this List<BackgroundJobSummary> jobs) => new()
    {
        Jobs = jobs.Select(j => new ListBackgroundJobsItem
        {
            Key = j.Key,
            Queued = j.Counters.Queued,
            Processed = j.Counters.Processed,
            Failed = j.Counters.Failed,
        }).ToList(),
    };
}
