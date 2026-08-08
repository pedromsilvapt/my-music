using Microsoft.AspNetCore.Mvc;
using MyMusic.Common;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.SongSharing;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("songs/{songId:long}/shares")]
public class SongSharingController(
    ICurrentUser currentUser,
    ISongShareService songShareService,
    ISharedSongImportService sharedSongImportService,
    ILogger<SongSharingController> logger) : ControllerBase
{
    [HttpGet("", Name = "ListSongShares")]
    public async Task<ActionResult<ListSongSharesResponse>> List(
        [FromRoute] long songId,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var shares = await songShareService.ListSharesAsync(db, songId, currentUser.Id, cancellationToken);

            return new ListSongSharesResponse
            {
                Shares = shares.Select(SongShareItem.FromDto).ToList(),
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("", Name = "CreateSongShare")]
    public async Task<ActionResult<CreateSongShareResponse>> Create(
        [FromRoute] long songId,
        [FromBody] CreateSongShareRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            // Idempotent: returns the existing share Id on duplicate without creating a new row.
            var shareId = await songShareService.CreateShareAsync(
                db, songId, request.UserId, currentUser.Id, cancellationToken);

            return new CreateSongShareResponse { ShareId = shareId };
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{userId:long}", Name = "DeleteSongShare")]
    public async Task<IActionResult> Delete(
        [FromRoute] long songId,
        [FromRoute] long userId,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            await songShareService.DeleteShareAsync(db, songId, userId, currentUser.Id, cancellationToken);

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Returns the distinct users who have shared at least one song with the current user.
    /// Top-level route (not song-scoped) — drives the client's "shared with me" sharer sub-menu.
    /// </summary>
    [HttpGet("/shares/sharers", Name = "ListSharers")]
    public async Task<ListSharersResponse> ListSharers(
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var sharers = await songShareService.ListSharersAsync(db, currentUser.Id, cancellationToken);

        return new ListSharersResponse
        {
            Sharers = sharers.Select(SongSharerItem.FromDto).ToList(),
        };
    }

    /// <summary>
    /// Imports a song shared with the current user into the current user's own library.
    /// Reuses the normal import pipeline (a fresh per-owner copy is created; re-import is
    /// idempotent via the <c>Skip</c> duplicate strategy). No <c>SongDevice</c> rows are
    /// created — this matches the normal <c>SongsController.Upload</c> behavior. The
    /// <c>SongSharing</c> row is intentionally kept so the recipient retains the shared view.
    /// </summary>
    [HttpPost("/songs/{songId:long}/import", Name = "ImportSharedSong")]
    public async Task<ActionResult<ImportSharedSongResponse>> Import(
        [FromRoute] long songId,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sharedSongImportService.ImportAsync(db, songId, currentUser.Id, cancellationToken);
            return ImportSharedSongResponse.FromResult(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Lists all shares for the given songs in one call. Owner-only. Used by the multi-song
    /// "Manage Sharing" dialog to compute per-user match counts across the selected songs.
    /// </summary>
    [HttpGet("/songs/shares", Name = "ListSongSharesBatch")]
    public async Task<ActionResult<ListSongSharesBatchResponse>> ListBatch(
        [FromQuery] string songIds,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var parsedSongIds = ParseSongIds(songIds);
        if (parsedSongIds.Length == 0)
            return BadRequest("At least one songId is required.");

        try
        {
            var shares = await songShareService.ListSharesForSongsAsync(
                db, parsedSongIds, currentUser.Id, cancellationToken);

            return new ListSongSharesBatchResponse
            {
                Shares = shares.Select(SongShareBatchItem.FromDto).ToList(),
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Batch upsert/delete of shares across multiple songs. Owner-only. Idempotent —
    /// duplicate Adds are no-ops, missing Removes are no-ops. Returns HTTP 200 (not 201)
    /// matching the per-song <c>CreateSongShare</c> idempotent semantics.
    /// </summary>
    [HttpPost("/songs/manage-shares", Name = "ManageSongShares")]
    public async Task<ActionResult<ManageSongSharesResponse>> Manage(
        [FromBody] ManageSongSharesRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryParseActions(request.Shares, out var actions, out var actionError))
            return BadRequest(actionError);

        try
        {
            var (created, removed) = await songShareService.ManageSharesAsync(
                db, request.SongIds, actions!, currentUser.Id, cancellationToken);

            return new ManageSongSharesResponse { Created = created, Removed = removed };
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    private static long[] ParseSongIds(string? songIds)
    {
        if (string.IsNullOrWhiteSpace(songIds))
            return [];

        var result = new List<long>();
        foreach (var part in songIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, out var id))
                result.Add(id);
        }
        return result.ToArray();
    }

    private static bool TryParseActions(
        List<SongShareActionItem> items,
        out List<SongShareAction>? actions,
        out string? error)
    {
        actions = null;
        error = null;
        var parsed = new List<SongShareAction>();
        foreach (var item in items)
        {
            if (!Enum.TryParse<SongShareActionType>(item.Action, ignoreCase: true, out var actionType))
            {
                error = $"Invalid action '{item.Action}'. Expected 'Add' or 'Remove'.";
                return false;
            }
            parsed.Add(new SongShareAction { UserId = item.UserId, Action = actionType });
        }
        actions = parsed;
        return true;
    }
}