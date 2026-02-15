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
    public async Task<ListPlaylistsResponse> List(MusicDbContext context, CancellationToken cancellationToken)
    {
        var playlists = await context.Playlists
            .Where(p => p.OwnerId == currentUser.Id)
            .Include(p => p.PlaylistSongs)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return new ListPlaylistsResponse
        {
            Playlists = playlists.Select(ListPlaylistItem.FromEntity).ToList()
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
            OwnerId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            PlaylistSongs = []
        };

        context.Playlists.Add(playlist);
        await context.SaveChangesAsync(cancellationToken);

        return new CreatePlaylistResponse
        {
            Playlist = CreatePlaylistItem.FromEntity(playlist)
        };
    }

    [HttpGet("{id:long}", Name = "GetPlaylist")]
    public async Task<GetPlaylistResponse> Get(
        [FromRoute] long id,
        MusicDbContext context,
        CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
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

        if (playlist == null)
        {
            throw new Exception($"Playlist not found with id {id}");
        }

        return new GetPlaylistResponse
        {
            Playlist = GetPlaylistItem.FromEntity(playlist)
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

        playlist.Name = request.Name;
        playlist.ModifiedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return new UpdatePlaylistResponse
        {
            Playlist = UpdatePlaylistItem.FromEntity(playlist)
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

        var maxOrder = playlist.PlaylistSongs.Any() ? playlist.PlaylistSongs.Max(ps => ps.Order) : 0;

        foreach (var songId in newSongIds)
        {
            var playlistSong = new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = songId,
                Order = ++maxOrder,
                AddedAt = DateTime.UtcNow
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
                var maxOrder = playlist.PlaylistSongs.Any() ? playlist.PlaylistSongs.Max(ps => ps.Order) : 0;

                foreach (var songId in songIdsToAdd)
                {
                    var playlistSong = new PlaylistSong
                    {
                        PlaylistId = playlist.Id,
                        SongId = songId,
                        Order = ++maxOrder,
                        AddedAt = DateTime.UtcNow
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
}