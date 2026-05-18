using Microsoft.AspNetCore.Mvc;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Admin;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("admin")]
public class AdminController(
    ICountRecalculationService countRecalculationService,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpPost("recalculate-counts", Name = "RecalculateCounts")]
    public async Task<RecalculateCountsResponse> RecalculateCounts(CancellationToken cancellationToken)
    {
        logger.LogInformation("Count recalculation requested via admin endpoint");
        var result = await countRecalculationService.RecalculateAllCountsAsync(cancellationToken);
        return result.ToResponse();
    }
}