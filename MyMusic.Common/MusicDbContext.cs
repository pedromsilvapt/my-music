using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common;

public class MusicDbContext : DbContext
{
    protected MusicDbContext() { }
    public MusicDbContext(DbContextOptions options) : base(options) { }
    public DbSet<Album> Albums { get; set; } = null!;

    public DbSet<AlbumSource> AlbumSources { get; set; } = null!;

    public DbSet<Artist> Artists { get; set; } = null!;

    public DbSet<ArtistSource> ArtistSources { get; set; } = null!;

    public DbSet<Artwork> Artworks { get; set; } = null!;

    public DbSet<Device> Devices { get; set; } = null!;

    public DbSet<Genre> Genres { get; set; } = null!;

    public DbSet<Playlist> Playlists { get; set; } = null!;

    public DbSet<PlaylistSong> PlaylistSongs { get; set; } = null!;

    public DbSet<PurchasedSong> PurchasedSongs { get; set; } = null!;

    public DbSet<Song> Songs { get; set; } = null!;

    public DbSet<SongArtist> SongArtists { get; set; } = null!;

    public DbSet<SongDevice> SongDevices { get; set; } = null!;

    public DbSet<SongGenre> SongGenres { get; set; } = null!;

    public DbSet<SongSource> SongSources { get; set; } = null!;

    public DbSet<Source> Sources { get; set; } = null!;

    public DbSet<User> Users { get; set; } = null!;

    public DbSet<DeviceSyncSession> DeviceSyncSessions { get; set; } = null!;

    public DbSet<DeviceSyncSessionRecord> DeviceSyncSessionRecords { get; set; } = null!;
}