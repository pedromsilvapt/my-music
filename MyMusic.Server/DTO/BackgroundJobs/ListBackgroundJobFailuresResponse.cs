using MyMusic.Common.Services.BackgroundJobs;

namespace MyMusic.Server.DTO.BackgroundJobs;

public record ListBackgroundJobFailuresResponse
{
    /// <summary>
    /// The number of failures across all pages.
    /// </summary>
    public required int Total { get; set; }

    public required List<ListBackgroundJobFailuresItem> Failures { get; set; }
}

public record ListBackgroundJobFailuresItem
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string? Message { get; set; }
    public required DateTime? OccurredAt { get; set; }
    public required List<ListBackgroundJobFailureDetail> Details { get; set; }
}

public record ListBackgroundJobFailureDetail
{
    public required string Label { get; set; }
    public required string? Value { get; set; }
}

public static class ListBackgroundJobFailuresResponseMapping
{
    public static ListBackgroundJobFailuresResponse ToResponse(this BackgroundJobFailurePage page) => new()
    {
        Total = page.Total,
        Failures = page.Items.Select(f => new ListBackgroundJobFailuresItem
        {
            Id = f.Id,
            Title = f.Title,
            Message = f.Message,
            OccurredAt = f.OccurredAt,
            Details = f.Details
                .Select(d => new ListBackgroundJobFailureDetail { Label = d.Label, Value = d.Value })
                .ToList(),
        }).ToList(),
    };
}
