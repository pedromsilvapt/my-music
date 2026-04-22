using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.PlaylistSongs;

public class PlaylistSongSkipService : IPlaylistSongSkipService
{
    public async Task SetSkipNextPlayback(MusicDbContext db, long playlistId, long songId, bool skipNextPlayback,
        long userId, CancellationToken cancellationToken = default)
    {
        var playlistSong = await db.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlistId && ps.SongId == songId && ps.Playlist.OwnerId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (playlistSong == null)
        {
            var playlistExists = await db.Playlists.AnyAsync(p => p.Id == playlistId, cancellationToken);
            if (!playlistExists)
            {
                throw new KeyNotFoundException($"Playlist not found with id {playlistId}");
            }

            var ownsPlaylist = await db.Playlists.AnyAsync(p => p.Id == playlistId && p.OwnerId == userId, cancellationToken);
            if (!ownsPlaylist)
            {
                throw new UnauthorizedAccessException($"You do not have access to playlist {playlistId}");
            }

            throw new KeyNotFoundException($"Song {songId} not found in playlist {playlistId}");
        }

        playlistSong.SkipNextPlayback = skipNextPlayback;
        if (skipNextPlayback)
        {
            playlistSong.StopAfterPlayback = false;
        }
        playlistSong.Playlist.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task BatchSetSkipNextPlayback(MusicDbContext db, long playlistId, List<long> songIds,
        bool skipNextPlayback, long userId, CancellationToken cancellationToken = default)
    {
        if (songIds.Count == 0)
        {
            throw new ArgumentException("songIds cannot be empty");
        }

        var playlist = await db.Playlists
            .Where(p => p.Id == playlistId && p.OwnerId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (playlist == null)
        {
            var playlistExists = await db.Playlists.AnyAsync(p => p.Id == playlistId, cancellationToken);
            if (!playlistExists)
            {
                throw new KeyNotFoundException($"Playlist not found with id {playlistId}");
            }

            throw new UnauthorizedAccessException($"You do not have access to playlist {playlistId}");
        }

        var playlistSongs = await db.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlistId && songIds.Contains(ps.SongId))
            .ToListAsync(cancellationToken);

        var foundSongIds = playlistSongs.Select(ps => ps.SongId).ToHashSet();
        var missingSongIds = songIds.Where(id => !foundSongIds.Contains(id)).ToList();
        if (missingSongIds.Count > 0)
        {
            throw new ArgumentException($"Songs not found in playlist: [{string.Join(", ", missingSongIds)}]");
        }

        foreach (var ps in playlistSongs)
        {
            ps.SkipNextPlayback = skipNextPlayback;
            if (skipNextPlayback)
            {
                ps.StopAfterPlayback = false;
            }
        }

        playlist.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
