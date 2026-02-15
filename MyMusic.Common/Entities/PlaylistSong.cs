namespace MyMusic.Common.Entities;

public class PlaylistSong
{
    public long Id { get; set; }

    public Playlist Playlist { get; set; } = null!;
    public long PlaylistId { get; set; }

    public Song Song { get; set; } = null!;
    public long SongId { get; set; }

    public int Order { get; set; }

    public DateTime AddedAt { get; set; }
}