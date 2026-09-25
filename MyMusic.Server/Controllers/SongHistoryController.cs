using Microsoft.AspNetCore.Mvc;

using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.Controllers;

/// <summary>
/// Read-only song revision history endpoints. Lives under the <c>songs</c> route prefix as a
/// sub-resource: <c>/songs/{songId}/history</c>. Only the song owner can view history.
/// </summary>
[ApiController]
[Route("songs/{songId:long}/history")]
public class SongHistoryController(
    ISongHistoryQueryService songHistoryQueryService,
    ISongHistoryVersionDiffService songHistoryVersionDiffService) : ControllerBase
{
    [HttpGet(Name = "GetSongHistory")]
    public async Task<GetSongHistoryResponse> GetHistory(
        long songId,
        CancellationToken cancellationToken)
    {
        var history = await songHistoryQueryService.GetSongHistoryAsync(songId, cancellationToken);

        return new GetSongHistoryResponse
        {
            History = history.Select(GetSongHistoryItem.FromEntity).ToList(),
        };
    }

    /// <summary>
    /// Returns the field-by-field metadata diff between the selected history revision
    /// (<paramref name="historyId"/>) and its immediate predecessor. Returns
    /// <see cref="NotFoundResult"/> when the song or history entry cannot be found, or
    /// when the song does not belong to the current user.
    /// </summary>
    [HttpGet("{historyId:long}/diff", Name = "GetSongHistoryDiff")]
    public async Task<ActionResult<SongHistoryDiffResponse>> GetHistoryDiff(
        long songId,
        long historyId,
        CancellationToken cancellationToken)
    {
        var result = await songHistoryVersionDiffService.GetVersionDiffAsync(
            songId, historyId, cancellationToken);

        if (result == null)
        {
            return NotFound($"Song history entry not found with id {historyId} for song {songId}");
        }

        return Ok(SongHistoryDiffResponse.FromResult(result));
    }
}