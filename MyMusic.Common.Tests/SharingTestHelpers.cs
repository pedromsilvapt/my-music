using MyMusic.Common.Entities;

namespace MyMusic.Common.Tests;

/// <summary>
/// Helpers to seed playlist shares. Songs are shared implicitly by adding them to a playlist
/// owned by the songs' owner and sharing that playlist with the recipient.
/// </summary>
public static class SharingTestHelpers
{
    /// <summary>
    /// Creates a new playlist owned by the owner of <paramref name="songs"/>, containing them,
    /// and shares it with <paramref name="recipient"/>.
    /// </summary>
    public static PlaylistSharing ShareSongs(MusicDbContext db, User recipient, params Song[] songs)
    {
        var playlist = new Playlist
        {
            Name = $"Shared with {recipient.Username}",
            OwnerId = songs[0].OwnerId,
            Type = PlaylistType.Playlist,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            PlaylistSongs = [],
        };

        var order = 1000.0;
        foreach (var song in songs)
        {
            playlist.PlaylistSongs.Add(new PlaylistSong
            {
                SongId = song.Id,
                Order = order,
                AddedAt = DateTime.UtcNow,
            });
            order += 1000.0;
        }

        db.Playlists.Add(playlist);
        return SharePlaylist(db, playlist, recipient);
    }

    /// <summary>
    /// Shares an existing <paramref name="playlist"/> with <paramref name="recipient"/>.
    /// </summary>
    public static PlaylistSharing SharePlaylist(MusicDbContext db, Playlist playlist, User recipient)
    {
        var sharing = new PlaylistSharing
        {
            Playlist = playlist,
            UserId = recipient.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.PlaylistSharings.Add(sharing);
        db.SaveChanges();
        return sharing;
    }
}
