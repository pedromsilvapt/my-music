using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
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
        [FromQuery] bool includeSystem = false)
    {
        var query = context.Playlists
            .Where(p => p.OwnerId == currentUser.Id);

        if (!includeSystem)
        {
            query = query.Where(p => p.Type == PlaylistType.Playlist);
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

        var maxOrder = playlist.PlaylistSongs.Any() ? playlist.PlaylistSongs.Max(ps => ps.Order) : -1;

        foreach (var songId in newSongIds)
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
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Queue, cancellationToken);
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
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Queue, cancellationToken);
        await LoadPlaylistWithSongs(context, playlist.Id, cancellationToken);

        context.PlaylistSongs.RemoveRange(playlist.PlaylistSongs);
        playlist.PlaylistSongs.Clear();

        for (var i = 0; i < request.SongIds.Count; i++)
        {
            playlist.PlaylistSongs.Add(new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = request.SongIds[i],
                Order = i,
                AddedAt = DateTime.UtcNow,
            });
        }

        playlist.CurrentSongId = request.CurrentSongId;
        playlist.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await GetQueue(context, cancellationToken);
    }

    [HttpPut("queue/current-song", Name = "SetQueueCurrentSong")]
    public async Task<GetPlaylistResponse> SetQueueCurrentSong(
        [FromBody] SetCurrentSongRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Queue, cancellationToken);
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
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Queue, cancellationToken);
        await context.Entry(playlist).Collection(p => p.PlaylistSongs).LoadAsync(cancellationToken);

        var existingSongIds = playlist.PlaylistSongs.Select(ps => ps.SongId).ToHashSet();
        var newSongIds = request.SongIds.Where(songId => !existingSongIds.Contains(songId)).ToList();

        if (newSongIds.Count == 0)
        {
            return await GetQueue(context, cancellationToken);
        }

        var maxOrder = playlist.PlaylistSongs.Any() ? playlist.PlaylistSongs.Max(ps => ps.Order) : -1;

        int insertIndex;
        if (request.Position == AddToQueuePosition.Now || request.Position == AddToQueuePosition.Next)
        {
            insertIndex = playlist.CurrentSongId.HasValue
                ? playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == playlist.CurrentSongId)?.Order + 1 ?? 0
                : 0;
        }
        else
        {
            insertIndex = maxOrder + 1;
        }

        if (request.Position == AddToQueuePosition.Now && newSongIds.Count > 0)
        {
            playlist.CurrentSongId = newSongIds[0];
        }

        var songsToShift = playlist.PlaylistSongs
            .Where(ps => ps.Order >= insertIndex)
            .OrderBy(ps => ps.Order)
            .ToList();

        foreach (var song in songsToShift)
        {
            song.Order += newSongIds.Count;
        }

        for (var i = 0; i < newSongIds.Count; i++)
        {
            playlist.PlaylistSongs.Add(new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = newSongIds[i],
                Order = insertIndex + i,
                AddedAt = DateTime.UtcNow,
            });
        }

        playlist.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await GetQueue(context, cancellationToken);
    }

    [HttpDelete("queue/songs", Name = "RemoveFromQueue")]
    public async Task<GetPlaylistResponse> RemoveFromQueue(
        [FromBody] RemoveFromQueueRequest request,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Queue, cancellationToken);
        await context.Entry(playlist).Collection(p => p.PlaylistSongs).LoadAsync(cancellationToken);

        var songIdsToRemove = request.SongIds.ToHashSet();
        var songsToRemove = playlist.PlaylistSongs
            .Where(ps => songIdsToRemove.Contains(ps.SongId))
            .ToList();

        if (songsToRemove.Any())
        {
            context.PlaylistSongs.RemoveRange(songsToRemove);
            playlist.PlaylistSongs.RemoveAll(ps => songIdsToRemove.Contains(ps.SongId));

            var remainingSongs = playlist.PlaylistSongs.OrderBy(ps => ps.Order).ToList();
            for (var i = 0; i < remainingSongs.Count; i++)
            {
                remainingSongs[i].Order = i;
            }

            if (playlist.CurrentSongId.HasValue && songIdsToRemove.Contains(playlist.CurrentSongId.Value))
            {
                var currentOrder = songsToRemove.FirstOrDefault(ps => ps.SongId == playlist.CurrentSongId)?.Order ?? 0;
                var nextSong = playlist.PlaylistSongs.FirstOrDefault(ps => ps.Order >= currentOrder);
                playlist.CurrentSongId = nextSong?.SongId;
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
        var playlist = await GetOrCreateSystemPlaylist(context, PlaylistType.Queue, cancellationToken);
        await context.Entry(playlist).Collection(p => p.PlaylistSongs).LoadAsync(cancellationToken);

        var songDict = playlist.PlaylistSongs.ToDictionary(ps => ps.Order);

        foreach (var reorder in request.Reorders)
        {
            if (!songDict.TryGetValue(reorder.FromIndex, out var song))
            {
                continue;
            }

            var songsToShift = playlist.PlaylistSongs
                .Where(ps => reorder.ToIndex <= reorder.FromIndex
                    ? ps.Order >= reorder.ToIndex && ps.Order < reorder.FromIndex
                    : ps.Order > reorder.FromIndex && ps.Order <= reorder.ToIndex)
                .ToList();

            foreach (var s in songsToShift)
            {
                s.Order += reorder.ToIndex <= reorder.FromIndex ? 1 : -1;
            }

            song.Order = reorder.ToIndex;
        }

        var remainingSongs = playlist.PlaylistSongs.OrderBy(ps => ps.Order).ToList();
        for (var i = 0; i < remainingSongs.Count; i++)
        {
            remainingSongs[i].Order = i;
        }

        playlist.ModifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await GetQueue(context, cancellationToken);
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
            .Include(p => p.PlaylistSongs)
            .ThenInclude(ps => ps.Song)
            .ThenInclude(s => s.Album)
            .ThenInclude(a => a.Artist)
            .Include(p => p.PlaylistSongs)
            .ThenInclude(ps => ps.Song)
            .ThenInclude(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(p => p.PlaylistSongs)
            .ThenInclude(ps => ps.Song)
            .ThenInclude(s => s.Genres)
            .ThenInclude(sg => sg.Genre)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
    }
}