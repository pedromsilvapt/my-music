using Microsoft.AspNetCore.Mvc;
using MyMusic.Common;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Playlists;
using MyMusic.Server.DTO.PlaylistSharing;
using MyMusic.Server.DTO.SongSharing;

namespace MyMusic.Server.Controllers;

/// <summary>
/// Playlist sharing endpoints. Sharing a playlist implicitly shares (read-only) every song in it
/// that is owned by the playlist owner — see <see cref="Common.Entities.Song.IsSharedWith"/>.
/// </summary>
[ApiController]
public class PlaylistSharingController(
    ICurrentUser currentUser,
    IPlaylistShareListService playlistShareListService,
    IPlaylistShareManageService playlistShareManageService,
    ISharerListService sharerListService,
    ISharedSongImportService sharedSongImportService) : ControllerBase
{
    /// <summary>
    /// Lists all shares for the given playlists in one call. Owner-only. Used by the
    /// "Share Playlists" dialog to compute per-user match counts across the selected playlists.
    /// </summary>
    [HttpGet("playlists/shares", Name = "ListPlaylistSharesBatch")]
    public async Task<ActionResult<ListPlaylistSharesBatchResponse>> ListBatch(
        [FromQuery] string playlistIds,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var parsedPlaylistIds = ParseIds(playlistIds);
        if (parsedPlaylistIds.Length == 0)
            return BadRequest("At least one playlistId is required.");

        try
        {
            var shares = await playlistShareListService.ListSharesAsync(
                db, parsedPlaylistIds, currentUser.Id, cancellationToken);

            return new ListPlaylistSharesBatchResponse
            {
                Shares = shares.Select(PlaylistShareBatchItem.FromDto).ToList(),
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
    /// Batch upsert/delete of shares across multiple playlists. Owner-only. Idempotent —
    /// duplicate Adds are no-ops, missing Removes are no-ops.
    /// </summary>
    [HttpPost("playlists/manage-shares", Name = "ManagePlaylistShares")]
    public async Task<ActionResult<ManagePlaylistSharesResponse>> Manage(
        [FromBody] ManagePlaylistSharesRequest request,
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryParseActions(request.Shares, out var actions, out var actionError))
            return BadRequest(actionError);

        try
        {
            var (created, removed) = await playlistShareManageService.ManageSharesAsync(
                db, request.PlaylistIds, actions!, currentUser.Id, cancellationToken);

            return new ManagePlaylistSharesResponse { Created = created, Removed = removed };
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
    /// Returns the distinct users who have shared at least one playlist with the current user.
    /// Drives the client's "shared with me" sharer sub-menu.
    /// </summary>
    [HttpGet("shares/sharers", Name = "ListSharers")]
    public async Task<ListSharersResponse> ListSharers(
        MusicDbContext db,
        CancellationToken cancellationToken)
    {
        var sharers = await sharerListService.ListSharersAsync(db, currentUser.Id, cancellationToken);

        return new ListSharersResponse
        {
            Sharers = sharers.Select(SongSharerItem.FromDto).ToList(),
        };
    }

    /// <summary>
    /// Imports a song shared with the current user into the current user's own library.
    /// Reuses the normal import pipeline (a fresh per-owner copy is created; re-import is
    /// idempotent via the <c>Skip</c> duplicate strategy). No <c>SongDevice</c> rows are
    /// created — this matches the normal <c>SongsController.Upload</c> behavior.
    /// </summary>
    [HttpPost("songs/{songId:long}/import", Name = "ImportSharedSong")]
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

    private static long[] ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return [];

        var result = new List<long>();
        foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, out var id))
                result.Add(id);
        }
        return result.ToArray();
    }

    private static bool TryParseActions(
        List<PlaylistShareActionItem> items,
        out List<PlaylistShareAction>? actions,
        out string? error)
    {
        actions = null;
        error = null;
        var parsed = new List<PlaylistShareAction>();
        foreach (var item in items)
        {
            if (!Enum.TryParse<PlaylistShareActionType>(item.Action, ignoreCase: true, out var actionType))
            {
                error = $"Invalid action '{item.Action}'. Expected 'Add' or 'Remove'.";
                return false;
            }
            parsed.Add(new PlaylistShareAction { UserId = item.UserId, Action = actionType });
        }
        actions = parsed;
        return true;
    }
}
