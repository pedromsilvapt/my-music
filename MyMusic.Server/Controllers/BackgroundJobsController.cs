using Microsoft.AspNetCore.Mvc;
using MyMusic.Common.Services;
using MyMusic.Common.Services.BackgroundJobs;
using MyMusic.Server.DTO.BackgroundJobs;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("background-jobs")]
public class BackgroundJobsController(ICurrentUser currentUser) : ControllerBase
{
    private const int MaxPageSize = 100;

    [HttpGet(Name = "ListBackgroundJobs")]
    public async Task<ListBackgroundJobsResponse> List(
        [FromServices] IBackgroundJobListService listService,
        CancellationToken cancellationToken)
    {
        var jobs = await listService.ListAsync(currentUser.Id, cancellationToken);

        return jobs.ToResponse();
    }

    [HttpGet("{key}/failures", Name = "ListBackgroundJobFailures")]
    public async Task<ActionResult<ListBackgroundJobFailuresResponse>> ListFailures(
        [FromServices] IBackgroundJobFailureListService failureListService,
        [FromRoute] string key,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            return BadRequest($"page must be at least 1 and pageSize between 1 and {MaxPageSize}");
        }

        try
        {
            var failures = await failureListService.ListAsync(key, currentUser.Id, page, pageSize, cancellationToken);

            return failures.ToResponse();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
