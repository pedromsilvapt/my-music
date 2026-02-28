using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.PlayHistory;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("play-history")]
public class PlayHistoryController(ICurrentUser currentUser) : ControllerBase
{
    [HttpPost(Name = "RecordPlayHistory")]
    public async Task<CreatePlayHistoryResponse> Create(
        [FromBody] CreatePlayHistoryRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var existing = await context.PlayHistories
            .Where(ph => ph.OwnerId == currentUser.Id && ph.ClientId == request.ClientId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
        {
            var playCount = await context.Songs
                .Where(s => s.Id == existing.SongId)
                .Select(s => s.PlayCount)
                .FirstOrDefaultAsync(cancellationToken);

            return new CreatePlayHistoryResponse
            {
                Created = false,
                Id = existing.Id,
                SongPlayCount = playCount,
            };
        }

        var song = await context.Songs
            .Where(s => s.Id == request.SongId && s.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (song == null)
        {
            throw new Exception($"Song not found with id {request.SongId}");
        }

        var playHistory = new PlayHistory
        {
            SongId = request.SongId,
            OwnerId = currentUser.Id,
            ClientId = request.ClientId,
            DeviceId = request.DeviceId,
            PlayedAt = DateTime.UtcNow,
        };

        song.PlayCount++;

        context.PlayHistories.Add(playHistory);
        await context.SaveChangesAsync(cancellationToken);

        return new CreatePlayHistoryResponse
        {
            Created = true,
            Id = playHistory.Id,
            SongPlayCount = song.PlayCount,
        };
    }

    [HttpGet(Name = "ListPlayHistory")]
    public async Task<ListPlayHistoryResponse> List(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] long? lastId = null,
        [FromQuery] int limit = 50,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] long? songId = null)
    {
        var query = context.PlayHistories
            .Where(ph => ph.OwnerId == currentUser.Id)
            .Include(ph => ph.Song)
            .ThenInclude(s => s.Album)
            .Include(ph => ph.Song)
            .ThenInclude(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .Include(ph => ph.Device)
            .AsSplitQuery();

        if (songId.HasValue)
        {
            query = query.Where(ph => ph.SongId == songId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(ph => ph.PlayedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(ph => ph.PlayedAt <= endDate.Value);
        }

        if (lastId.HasValue)
        {
            var lastPlayHistory = await context.PlayHistories
                .Where(ph => ph.Id == lastId.Value && ph.OwnerId == currentUser.Id)
                .Select(ph => ph.PlayedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastPlayHistory != default)
            {
                query = query.Where(ph => ph.PlayedAt < lastPlayHistory);
            }
        }

        var history = await query
            .OrderByDescending(ph => ph.PlayedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new ListPlayHistoryResponse
        {
            Items = history.Select(ListPlayHistoryItem.FromEntity).ToList(),
        };
    }
}