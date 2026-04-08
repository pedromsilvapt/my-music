using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Extensions;
using MyMusic.Common.Filters;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Filters;
using MyMusic.Server.DTO.Playlists;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("playlists")]
public class PlaylistsController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet(Name = "ListPlaylists")]
    public async Task<ListPlaylistsResponse> List(
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] bool includeSystem = false,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null)
    {
        var query = context.Playlists
            .Where(p => p.OwnerId == currentUser.Id);

        if (!includeSystem)
        {
            query = query.Where(p => p.Type == PlaylistType.Playlist);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = FuzzySearchHelper.ApplyFuzzySearch(query, search, p => p.SearchableText);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterRequest = FilterDslParser.Parse(filter);
            var filterExpression = DynamicFilterBuilder.BuildFilter<Playlist>(filterRequest);
            query = query.Where(filterExpression);
        }

        var playlists = await query
            .Include(p => p.PlaylistSongs)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return new ListPlaylistsResponse
        {
            Playlists = playlists.Select(ListPlaylistItem.FromEntity).ToList(),
        };
    }

    [HttpPost(Name = "CreatePlaylist")]
    public async Task<CreatePlaylistResponse> Create(
        [FromBody] CreatePlaylistRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = new Playlist
        {
            Name = request.Name,
            Type = PlaylistType.Playlist,
            OwnerId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            PlaylistSongs = [],
        };

        context.Playlists.Add(playlist);
        await context.SaveChangesAsync(cancellationToken);

        return new CreatePlaylistResponse
        {
            Playlist = CreatePlaylistItem.FromEntity(playlist),
        };
    }

    [HttpGet("{id:long}", Name = "GetPlaylist")]
    public async Task<GetPlaylistResponse> Get(
        [FromRoute] long id,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await LoadPlaylistWithSongs(context, id, cancellationToken);

        if (playlist == null)
        {
            throw new Exception($"Playlist not found with id {id}");
        }

        return new GetPlaylistResponse
        {
            Playlist = GetPlaylistItem.FromEntity(playlist),
        };
    }

    [HttpPut("{id:long}", Name = "UpdatePlaylist")]
    public async Task<UpdatePlaylistResponse> Update(
        [FromRoute] long id,
        [FromBody] UpdatePlaylistRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Where(p => p.Id == id && p.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (playlist == null)
        {
            throw new Exception($"Playlist not found with id {id}");
        }

        if (playlist.Type != PlaylistType.Playlist)
        {
            throw new Exception($"Cannot rename system playlist {playlist.Type}");
        }

        playlist.Name = request.Name;
        playlist.ModifiedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return new UpdatePlaylistResponse
        {
            Playlist = UpdatePlaylistItem.FromEntity(playlist),
        };
    }

    [HttpDelete("{id:long}", Name = "DeletePlaylist")]
    public async Task Delete(
        [FromRoute] long id,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Where(p => p.Id == id && p.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (playlist == null)
        {
            throw new Exception($"Playlist not found with id {id}");
        }

        if (playlist.Type != PlaylistType.Playlist)
        {
            throw new Exception($"Cannot delete system playlist {playlist.Type}");
        }

        context.Playlists.Remove(playlist);
        await context.SaveChangesAsync(cancellationToken);
    }

    [HttpPost("{id:long}/songs", Name = "AddSongsToPlaylist")]
    public async Task<GetPlaylistResponse> AddSongs(
        [FromRoute] long id,
        [FromBody] AddSongsToPlaylistRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Where(p => p.Id == id && p.OwnerId == currentUser.Id)
            .Include(p => p.PlaylistSongs)
            .FirstOrDefaultAsync(cancellationToken);

        if (playlist == null)
        {
            throw new Exception($"Playlist not found with id {id}");
        }

        var existingSongIds = playlist.PlaylistSongs.Select(ps => ps.SongId).ToHashSet();
        var newSongIds = request.SongIds.Where(songId => !existingSongIds.Contains(songId)).ToList();

        var maxOrder = playlist.PlaylistSongs.Any() ? playlist.PlaylistSongs.Max(ps => ps.Order) : 0.0;
        var nextOrder = maxOrder + 1000.0;

        foreach (var songId in newSongIds)
        {
            var playlistSong = new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = songId,
                Order = nextOrder,
                AddedAt = DateTime.UtcNow,
            };
            context.PlaylistSongs.Add(playlistSong);
            nextOrder += 1000.0;
        }

        playlist.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await Get(id, context, cancellationToken);
    }

    [HttpDelete("{id:long}/songs/{songId:long}", Name = "RemoveSongFromPlaylist")]
    public async Task<GetPlaylistResponse> RemoveSong(
        [FromRoute] long id,
        [FromRoute] long songId,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlistSong = await context.PlaylistSongs
            .Where(ps => ps.PlaylistId == id && ps.SongId == songId)
            .Include(ps => ps.Playlist)
            .FirstOrDefaultAsync(cancellationToken);

        if (playlistSong == null || playlistSong.Playlist.OwnerId != currentUser.Id)
        {
            throw new Exception($"Song {songId} not found in playlist {id}");
        }

        var playlist = await context.Playlists
            .Where(p => p.Id == id && p.OwnerId == currentUser.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (playlist == null)
        {
            throw new Exception($"Playlist not found with id {id}");
        }

        context.PlaylistSongs.Remove(playlistSong);
        playlist.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await Get(id, context, cancellationToken);
    }

    [HttpPost("manage-songs", Name = "ManagePlaylistSongs")]
    public async Task ManageSongs(
        [FromBody] ManagePlaylistSongsRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        foreach (var name in request.NewPlaylists.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var playlist = new Playlist
            {
                Name = name.Trim(),
                Type = PlaylistType.Playlist,
                OwnerId = currentUser.Id,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                PlaylistSongs = request.SongIds.Select((songId, index) => new PlaylistSong
                {
                    SongId = songId,
                    Order = index * 1000.0,
                    AddedAt = DateTime.UtcNow,
                }).ToList(),
            };

            context.Playlists.Add(playlist);
        }

        foreach (var playlistAction in request.Playlists)
        {
            var playlist = await context.Playlists
                .Where(p => p.Id == playlistAction.PlaylistId && p.OwnerId == currentUser.Id)
                .Include(p => p.PlaylistSongs)
                .FirstOrDefaultAsync(cancellationToken);

            if (playlist == null)
            {
                continue;
            }

            var existingSongIds = playlist.PlaylistSongs.Select(ps => ps.SongId).ToHashSet();

            if (playlistAction.Action == PlaylistAction.Add)
            {
                var songIdsToAdd = request.SongIds.Where(songId => !existingSongIds.Contains(songId)).ToList();
                var maxOrder = playlist.PlaylistSongs.Any() ? playlist.PlaylistSongs.Max(ps => ps.Order) : -1;

                foreach (var songId in songIdsToAdd)
                {
                    var playlistSong = new PlaylistSong
                    {
                        PlaylistId = playlist.Id,
                        SongId = songId,
                        Order = ++maxOrder,
                        AddedAt = DateTime.UtcNow,
                    };
                    context.PlaylistSongs.Add(playlistSong);
                }

                if (songIdsToAdd.Any())
                {
                    playlist.ModifiedAt = DateTime.UtcNow;
                }
            }
            else if (playlistAction.Action == PlaylistAction.Remove)
            {
                var songIdsToRemove = request.SongIds.Where(songId => existingSongIds.Contains(songId)).ToList();

                var playlistSongsToRemove = await context.PlaylistSongs
                    .Where(ps => ps.PlaylistId == playlist.Id && songIdsToRemove.Contains(ps.SongId))
                    .ToListAsync(cancellationToken);

                if (playlistSongsToRemove.Any())
                {
                    context.PlaylistSongs.RemoveRange(playlistSongsToRemove);
                    playlist.ModifiedAt = DateTime.UtcNow;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    [HttpGet("queue", Name = "GetQueue")]
    public async Task<GetPlaylistResponse> GetQueue(
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateCurrentQueue(context, cancellationToken);
        playlist = await LoadPlaylistWithSongs(context, playlist.Id, cancellationToken);

        return new GetPlaylistResponse
        {
            Playlist = GetPlaylistItem.FromEntity(playlist!),
        };
    }

    [HttpPut("queue", Name = "ReplaceQueue")]
    public async Task<GetPlaylistResponse> ReplaceQueue(
        [FromBody] ReplaceQueueRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateCurrentQueue(context, cancellationToken);
        await LoadPlaylistWithSongs(context, playlist.Id, cancellationToken);

        context.PlaylistSongs.RemoveRange(playlist.PlaylistSongs);
        playlist.PlaylistSongs.Clear();

        for (var i = 0; i < request.SongIds.Count; i++)
        {
            playlist.PlaylistSongs.Add(new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = request.SongIds[i],
                Order = (i + 1) * 1000.0,
                AddedAt = DateTime.UtcNow,
            });
        }

        playlist.CurrentSongId = request.CurrentSongId;
        playlist.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await GetQueue(context, cancellationToken);
    }

    [HttpPut("queues/{id:long}/current-song", Name = "SetQueueCurrentSongById")]
    public async Task<GetPlaylistResponse> SetQueueCurrentSongById(
        [FromRoute] long id,
        [FromBody] SetCurrentSongRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken);
        if (user == null)
        {
            throw new Exception($"User not found with id {currentUser.Id}");
        }

        var queue = await context.Playlists
            .Where(p => p.Id == id && p.OwnerId == currentUser.Id && p.Type == PlaylistType.Queue)
            .FirstOrDefaultAsync(cancellationToken);

        if (queue == null)
        {
            throw new Exception($"Queue not found with id {id}");
        }

        queue.CurrentSongId = request.CurrentSongId;
        queue.ModifiedAt = DateTime.UtcNow;
        user.CurrentQueueId = id;

        await context.SaveChangesAsync(cancellationToken);

        var playlist = await LoadPlaylistWithSongs(context, id, cancellationToken);

        return new GetPlaylistResponse
        {
            Playlist = GetPlaylistItem.FromEntity(playlist!),
        };
    }

    [HttpPut("queue/current-song", Name = "SetQueueCurrentSong")]
    public async Task<GetPlaylistResponse> SetQueueCurrentSong(
        [FromBody] SetCurrentSongRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateCurrentQueue(context, cancellationToken);
        playlist.CurrentSongId = request.CurrentSongId;
        playlist.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await GetQueue(context, cancellationToken);
    }

    [HttpPost("queue/songs", Name = "AddToQueue")]
    public async Task<GetPlaylistResponse> AddToQueue(
        [FromBody] AddToQueueRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateCurrentQueue(context, cancellationToken);
        await context.Entry(playlist).Collection(p => p.PlaylistSongs).LoadAsync(cancellationToken);

        var existingBySongId = playlist.PlaylistSongs.ToDictionary(ps => ps.SongId);
        var existingSongIdsInRequest = request.SongIds.Where(id => existingBySongId.ContainsKey(id)).ToList();
        var newSongIds = request.SongIds.Where(id => !existingBySongId.ContainsKey(id)).ToList();

        if (existingSongIdsInRequest.Count == 0 && newSongIds.Count == 0)
        {
            return await GetQueue(context, cancellationToken);
        }

        var maxOrder = playlist.PlaylistSongs.Any() ? playlist.PlaylistSongs.Max(ps => ps.Order) : 0.0;

        if (request.Position == AddToQueuePosition.Now)
        {
            if (existingSongIdsInRequest.Count > 0)
            {
                playlist.CurrentSongId = existingSongIdsInRequest[0];
            }
            else if (newSongIds.Count > 0)
            {
                playlist.CurrentSongId = newSongIds[0];
            }
        }

        double insertOrder;
        if (request.Position == AddToQueuePosition.Last)
        {
            insertOrder = maxOrder + 1000.0;
        }
        else
        {
            var songsByOrder = playlist.PlaylistSongs.OrderBy(ps => ps.Order).ToList();
            
            if (!playlist.CurrentSongId.HasValue || songsByOrder.Count == 0)
            {
                insertOrder = 1000.0;
            }
            else
            {
                var currentSongOrder = existingBySongId.TryGetValue(playlist.CurrentSongId.Value, out var existingPs)
                    ? existingPs.Order
                    : playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == playlist.CurrentSongId.Value)?.Order ?? 0;
                
                var currentIndex = songsByOrder.FindIndex(ps => ps.SongId == playlist.CurrentSongId.Value);
                
                if (currentIndex < 0 || currentIndex >= songsByOrder.Count - 1)
                {
                    insertOrder = currentSongOrder + 1000.0;
                }
                else
                {
                    var nextOrder = songsByOrder[currentIndex + 1].Order;
                    insertOrder = (currentSongOrder + nextOrder) / 2.0;
                }
            }
        }

        var existingOrdersToReposition = existingSongIdsInRequest
            .Select(id => existingBySongId[id])
            .OrderBy(ps => ps.Order)
            .ToList();

        var remainingSongs = playlist.PlaylistSongs
            .Where(ps => !existingSongIdsInRequest.Contains(ps.SongId))
            .OrderBy(ps => ps.Order)
            .ToList();

        var allOrders = remainingSongs.Select(ps => ps.Order).ToList();
        var needsRebalance = NeedsRebalance(allOrders) || allOrders.Count == 0;

        if (needsRebalance && remainingSongs.Count > 0)
        {
            var rebalancedOrders = RebalanceOrders(remainingSongs.Count);
            for (var i = 0; i < remainingSongs.Count; i++)
            {
                remainingSongs[i].Order = rebalancedOrders[i];
            }
            
            var currentSong = playlist.CurrentSongId.HasValue
                ? remainingSongs.FirstOrDefault(ps => ps.SongId == playlist.CurrentSongId.Value)
                : null;
            if (currentSong != null)
            {
                var currentIndex = remainingSongs.IndexOf(currentSong);
                if (currentIndex < remainingSongs.Count - 1)
                {
                    var nextOrder = remainingSongs[currentIndex + 1].Order;
                    insertOrder = (currentSong.Order + nextOrder) / 2.0;
                }
                else
                {
                    insertOrder = currentSong.Order + 1000.0;
                }
            }
            else
            {
                insertOrder = 1000.0;
            }
        }

        var songOrder = insertOrder;
        foreach (var ps in existingOrdersToReposition)
        {
            ps.Order = songOrder;
            songOrder += 0.001;
        }

        foreach (var songId in newSongIds)
        {
            playlist.PlaylistSongs.Add(new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = songId,
                Order = songOrder,
                AddedAt = DateTime.UtcNow,
            });
            songOrder += 0.001;
        }

        playlist.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        if (NeedsRebalance(playlist.PlaylistSongs.Select(ps => ps.Order).ToList()))
        {
            await RebalanceAndSave(playlist, context, cancellationToken);
        }

        return await GetQueue(context, cancellationToken);
    }

    private static bool NeedsRebalance(IReadOnlyList<double> orders)
    {
        if (orders.Count < 2) return false;
        
        var sortedOrders = orders.Order().ToList();
        for (var i = 1; i < sortedOrders.Count; i++)
        {
            var gap = sortedOrders[i] - sortedOrders[i - 1];
            if (gap < 0.001 && gap > 0)
            {
                return true;
            }
        }
        
        return false;
    }

    private static List<double> RebalanceOrders(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => (i + 1) * 1000.0)
            .ToList();
    }

    private async Task RebalanceAndSave(Playlist playlist, MusicDbContext context, CancellationToken cancellationToken)
    {
        var songs = playlist.PlaylistSongs.OrderBy(ps => ps.Order).ToList();
        var rebalancedOrders = RebalanceOrders(songs.Count);
        
        for (var i = 0; i < songs.Count; i++)
        {
            songs[i].Order = rebalancedOrders[i];
        }
        
        await context.SaveChangesAsync(cancellationToken);
    }

    [HttpDelete("queue/songs", Name = "RemoveFromQueue")]
    public async Task<GetPlaylistResponse> RemoveFromQueue(
        [FromBody] RemoveFromQueueRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateCurrentQueue(context, cancellationToken);
        await context.Entry(playlist).Collection(p => p.PlaylistSongs).LoadAsync(cancellationToken);

        var songIdsToRemove = request.SongIds.ToHashSet();
        var songsToRemove = playlist.PlaylistSongs
            .Where(ps => songIdsToRemove.Contains(ps.SongId))
            .ToList();

        if (songsToRemove.Any())
        {
            context.PlaylistSongs.RemoveRange(songsToRemove);
            playlist.PlaylistSongs.RemoveAll(ps => songIdsToRemove.Contains(ps.SongId));

            if (playlist.CurrentSongId.HasValue && songIdsToRemove.Contains(playlist.CurrentSongId.Value))
            {
                var removedSong = songsToRemove.FirstOrDefault(ps => ps.SongId == playlist.CurrentSongId);
                var removedOrder = removedSong?.Order ?? 0;
                
                var nextSong = playlist.PlaylistSongs
                    .OrderBy(ps => ps.Order)
                    .FirstOrDefault(ps => ps.Order > removedOrder);
                    
                playlist.CurrentSongId = nextSong?.SongId ?? playlist.PlaylistSongs.OrderBy(ps => ps.Order).FirstOrDefault()?.SongId;
            }

            playlist.ModifiedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return await GetQueue(context, cancellationToken);
    }

    [HttpPost("queue/reorder", Name = "ReorderQueue")]
    public async Task<GetPlaylistResponse> ReorderQueue(
        [FromBody] ReorderQueueRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateCurrentQueue(context, cancellationToken);
        await context.Entry(playlist).Collection(p => p.PlaylistSongs).LoadAsync(cancellationToken);

        var songsByOrder = playlist.PlaylistSongs.OrderBy(ps => ps.Order).ToList();

        foreach (var reorder in request.Reorders)
        {
            if (reorder.FromIndex < 0 || reorder.FromIndex >= songsByOrder.Count ||
                reorder.ToIndex < 0 || reorder.ToIndex >= songsByOrder.Count ||
                reorder.FromIndex == reorder.ToIndex)
            {
                continue;
            }

            var song = songsByOrder[reorder.FromIndex];

            double newOrder;
            if (reorder.ToIndex == 0)
            {
                newOrder = songsByOrder[0].Order - 1000.0;
            }
            else if (reorder.ToIndex == songsByOrder.Count - 1)
            {
                newOrder = songsByOrder[^1].Order + 1000.0;
            }
            else
            {
                var lowerIndex = Math.Min(reorder.ToIndex, reorder.FromIndex);
                if (reorder.FromIndex < reorder.ToIndex)
                {
                    lowerIndex = reorder.ToIndex;
                }
                
                var prevIndex = reorder.ToIndex > reorder.FromIndex ? reorder.ToIndex : Math.Max(0, reorder.ToIndex - 1);
                var nextIndex = reorder.ToIndex > reorder.FromIndex ? Math.Min(songsByOrder.Count - 1, reorder.ToIndex) : reorder.ToIndex;
                
                if (reorder.FromIndex < reorder.ToIndex)
                {
                    prevIndex = reorder.ToIndex;
                    nextIndex = Math.Min(reorder.ToIndex + 1, songsByOrder.Count - 1);
                }
                else
                {
                    prevIndex = Math.Max(0, reorder.ToIndex - 1);
                    nextIndex = reorder.ToIndex;
                }

                var prevOrder = songsByOrder[prevIndex].Order;
                var nextOrder = songsByOrder[nextIndex].Order;
                
                newOrder = (prevOrder + nextOrder) / 2.0;
            }

            song.Order = newOrder;
            songsByOrder = playlist.PlaylistSongs.OrderBy(ps => ps.Order).ToList();
        }

        if (NeedsRebalance(playlist.PlaylistSongs.Select(ps => ps.Order).ToList()))
        {
            await RebalanceAndSave(playlist, context, cancellationToken);
        }
        else
        {
            playlist.ModifiedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return await GetQueue(context, cancellationToken);
    }

    [HttpPost("queue/shuffle", Name = "ShuffleQueue")]
    public async Task<GetPlaylistResponse> ShuffleQueue(
        [FromBody] ShuffleQueueRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateCurrentQueue(context, cancellationToken);
        await context.Entry(playlist).Collection(p => p.PlaylistSongs).LoadAsync(cancellationToken);

        if (request.Indices.Count < 2)
        {
            return await GetQueue(context, cancellationToken);
        }

        var songsByOrder = playlist.PlaylistSongs.OrderBy(ps => ps.Order).ToList();
        var songsAtIndices = request.Indices
            .Where(i => i >= 0 && i < songsByOrder.Count)
            .Select(i => songsByOrder[i])
            .ToList();

        if (songsAtIndices.Count < 2)
        {
            return await GetQueue(context, cancellationToken);
        }

        var orders = songsAtIndices.Select(s => s.Order).ToList();
        Shuffle(orders);

        for (var i = 0; i < songsAtIndices.Count; i++)
        {
            songsAtIndices[i].Order = orders[i];
        }

        if (NeedsRebalance(playlist.PlaylistSongs.Select(ps => ps.Order).ToList()))
        {
            await RebalanceAndSave(playlist, context, cancellationToken);
        }
        else
        {
            playlist.ModifiedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return await GetQueue(context, cancellationToken);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        var random = Random.Shared;
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = random.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    [HttpGet("favorites", Name = "GetFavorites")]
    public async Task<GetPlaylistResponse> GetFavorites(
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Favorites, cancellationToken);
        playlist = await LoadPlaylistWithSongs(context, playlist.Id, cancellationToken);

        return new GetPlaylistResponse
        {
            Playlist = GetPlaylistItem.FromEntity(playlist!),
        };
    }

    [HttpPost("favorites/songs", Name = "AddToFavorites")]
    public async Task<GetPlaylistResponse> AddToFavorites(
        [FromBody] AddSongsToPlaylistRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Favorites, cancellationToken);
        return await AddSongs(playlist.Id, request, context, cancellationToken);
    }

    [HttpDelete("favorites/songs/{songId:long}", Name = "RemoveFromFavorites")]
    public async Task<GetPlaylistResponse> RemoveFromFavorites(
        [FromRoute] long songId,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Favorites, cancellationToken);
        return await RemoveSong(playlist.Id, songId, context, cancellationToken);
    }

    [HttpGet("queues", Name = "ListQueues")]
    public async Task<ListQueuesResponse> ListQueues(
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var queues = await context.Playlists
            .Where(p => p.OwnerId == currentUser.Id && p.Type == PlaylistType.Queue)
            .Include(p => p.PlaylistSongs)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return new ListQueuesResponse
        {
            Queues = queues.Select(ListQueueItem.FromEntity).ToList(),
        };
    }

    [HttpPost("queues", Name = "CreateQueue")]
    public async Task<CreateQueueResponse> CreateQueue(
        [FromBody] CreateQueueRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken);
        if (user == null)
        {
            throw new Exception($"User not found with id {currentUser.Id}");
        }

        var name = request.Name;
        if (string.IsNullOrEmpty(name))
        {
            name = $"Queue ({DateTime.UtcNow:MMM d, yyyy})";
        }

        var currentSongId = request.CurrentSongId;
        if (currentSongId == null && request.SongIds.Count > 0)
        {
            currentSongId = request.SongIds[0];
        }

        var playlist = new Playlist
        {
            Name = name,
            Type = PlaylistType.Queue,
            OwnerId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            PlaylistSongs = [],
            CurrentSongId = currentSongId,
        };

        context.Playlists.Add(playlist);
        await context.SaveChangesAsync(cancellationToken);

        var order = 1000.0;
        foreach (var songId in request.SongIds)
        {
            context.PlaylistSongs.Add(new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = songId,
                Order = order,
                AddedAt = DateTime.UtcNow,
            });
            order += 1000.0;
        }

        await context.SaveChangesAsync(cancellationToken);

        user.CurrentQueueId = playlist.Id;
        await context.SaveChangesAsync(cancellationToken);

        var loadedPlaylist = await LoadPlaylistWithSongs(context, playlist.Id, cancellationToken);
        return new CreateQueueResponse
        {
            Queue = GetPlaylistItem.FromEntity(loadedPlaylist!),
        };
    }

    [HttpPut("queues/{id:long}", Name = "RenameQueue")]
    public async Task<RenameQueueResponse> RenameQueue(
        [FromRoute] long id,
        [FromBody] RenameQueueRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var queue = await context.Playlists
            .Where(p => p.Id == id && p.OwnerId == currentUser.Id && p.Type == PlaylistType.Queue)
            .Include(p => p.PlaylistSongs)
            .FirstOrDefaultAsync(cancellationToken);

        if (queue == null)
        {
            throw new Exception($"Queue not found with id {id}");
        }

        queue.Name = request.Name;
        queue.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new RenameQueueResponse
        {
            Queue = ListQueueItem.FromEntity(queue),
        };
    }

    [HttpDelete("queues/{id:long}", Name = "DeleteQueue")]
    public async Task<ActionResult> DeleteQueue(
        [FromRoute] long id,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken);
        if (user == null)
        {
            throw new Exception($"User not found with id {currentUser.Id}");
        }

        var queue = await context.Playlists
            .Where(p => p.Id == id && p.OwnerId == currentUser.Id && p.Type == PlaylistType.Queue)
            .Include(p => p.PlaylistSongs)
            .FirstOrDefaultAsync(cancellationToken);

        if (queue == null)
        {
            throw new Exception($"Queue not found with id {id}");
        }

        var wasCurrentQueue = user.CurrentQueueId == queue.Id;

        context.PlaylistSongs.RemoveRange(queue.PlaylistSongs);
        context.Playlists.Remove(queue);

        if (wasCurrentQueue)
        {
            var nextQueue = await context.Playlists
                .Where(p => p.OwnerId == currentUser.Id && p.Type == PlaylistType.Queue && p.Id != queue.Id)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            user.CurrentQueueId = nextQueue?.Id;
        }

        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<Playlist> GetOrCreateCurrentQueue(
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FindAsync([currentUser.Id], cancellationToken);
        if (user == null)
        {
            throw new Exception($"User not found with id {currentUser.Id}");
        }

        if (user.CurrentQueueId.HasValue)
        {
            var existingQueue = await context.Playlists
                .Where(p => p.Id == user.CurrentQueueId.Value && p.OwnerId == currentUser.Id && p.Type == PlaylistType.Queue)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingQueue != null)
            {
                return existingQueue;
            }
        }

        var playlist = new Playlist
        {
            Name = $"Queue ({DateTime.UtcNow:MMM d, yyyy})",
            Type = PlaylistType.Queue,
            OwnerId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            PlaylistSongs = [],
        };

        context.Playlists.Add(playlist);
        await context.SaveChangesAsync(cancellationToken);

        user.CurrentQueueId = playlist.Id;
        await context.SaveChangesAsync(cancellationToken);

        return playlist;
    }

    private async Task<Playlist> GetOrCreateSystemPlaylist(
        MusicDbContext context,
        PlaylistType type,
        CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Where(p => p.OwnerId == currentUser.Id && p.Type == type)
            .FirstOrDefaultAsync(cancellationToken);

        if (playlist != null)
        {
            return playlist;
        }

        playlist = new Playlist
        {
            Name = type.ToString(),
            Type = type,
            OwnerId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            PlaylistSongs = [],
        };

        context.Playlists.Add(playlist);
        await context.SaveChangesAsync(cancellationToken);

        return playlist;
    }

    private async Task<Playlist?> LoadPlaylistWithSongs(
        MusicDbContext context,
        long id,
        CancellationToken cancellationToken)
    {
        return await context.Playlists
            .Where(p => p.Id == id && p.OwnerId == currentUser.Id)
            .IncludeSongMetadata("PlaylistSongs.Song")
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
    }

    [HttpGet("filter-metadata", Name = "GetPlaylistFilterMetadata")]
    public FilterMetadataResponse GetFilterMetadata() =>
        new()
        {
            Fields =
            [
                new FilterFieldMetadata
                {
                    Name = "name",
                    Type = "string",
                    Description = "Playlist name",
                    SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith", "isNull", "isNotNull"],
                    SupportsDynamicValues = true,
                },
                new FilterFieldMetadata
                {
                    Name = "type",
                    Type = "string",
                    Description = "Playlist type (Playlist, Queue, Favorites)",
                    SupportedOperators = ["eq", "neq"],
                    Values = ["Playlist", "Queue", "Favorites"],
                },
                new FilterFieldMetadata
                {
                    Name = "createdAt",
                    Type = "date",
                    Description = "Date created",
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "isNull", "isNotNull"],
                },
                new FilterFieldMetadata
                {
                    Name = "modifiedAt",
                    Type = "date",
                    Description = "Date last modified",
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "isNull", "isNotNull"],
                },
                new FilterFieldMetadata
                {
                    Name = "songCount",
                    Type = "number",
                    Description = "Number of songs",
                    IsComputed = true,
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
                },
                new FilterFieldMetadata
                {
                    Name = "totalDurationSeconds",
                    Type = "number",
                    Description = "Total duration in seconds",
                    IsComputed = true,
                    SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
                },
                new FilterFieldMetadata
                {
                    Name = "searchableText",
                    Type = "string",
                    Description = "Combined searchable text",
                    IsComputed = true,
                    SupportedOperators = ["contains"],
                },
            ],
            Operators = FilterMetadataHelper.GetOperatorMetadata(),
        };

    [HttpGet("filter-values", Name = "GetPlaylistFilterValues")]
    public async Task<FilterValuesResponse> GetFilterValues(
        [FromQuery] string field,
        MusicDbContext context,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 15)
    {
        var query = field switch
        {
            "name" => context.Playlists
                .Where(p => p.OwnerId == currentUser.Id)
                .Select(p => p.Name)
                .Distinct(),
            _ => Enumerable.Empty<string>().AsQueryable(),
        };

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(v => v.ToLower().Contains(searchLower));
        }

        var values = await query
            .OrderBy(v => v)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new FilterValuesResponse { Values = values };
    }
}
