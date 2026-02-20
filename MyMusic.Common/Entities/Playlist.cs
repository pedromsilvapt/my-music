using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class Playlist
{
    public long Id { get; set; }

    [MaxLength(256)] public required string Name { get; set; }

    public PlaylistType Type { get; set; } = PlaylistType.Playlist;

    public User Owner { get; set; } = null!;
    public long OwnerId { get; set; }

    public Song? CurrentSong { get; set; }
    public long? CurrentSongId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public required List<PlaylistSong> PlaylistSongs { get; set; }
}