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

    public DbSet<PlayHistory> PlayHistories { get; set; } = null!;

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

    public DbSet<AuditNonConformity> AuditNonConformities { get; set; } = null!;

    public DbSet<AutoFetchedMetadata> AutoFetchedMetadata { get; set; } = null!;

    public DbSet<MetadataFetchTask> MetadataFetchTasks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AutoFetchedMetadata entity configuration
        modelBuilder.Entity<AutoFetchedMetadata>(entity =>
        {
            // Store JSON data
            entity.Property(e => e.SourceMetadata).HasColumnType("jsonb");

            // For querying by song + status (finding pending metadata)
            entity.HasIndex(e => new { e.SongId, e.Status });

            // For 30-day window queries (fetch deduplication)
            entity.HasIndex(e => new { e.SongId, e.FetchedAt });

            // For status-based cleanup (expired records)
            entity.HasIndex(e => new { e.Status, e.FetchedAt });
        });

        // MetadataFetchTask entity configuration
        modelBuilder.Entity<MetadataFetchTask>(entity =>
        {
            // For pulling queued tasks (scheduler)
            entity.HasIndex(e => new { e.Status, e.CreatedAt });

            // For querying by song (checking if already queued)
            entity.HasIndex(e => new { e.SongId, e.Status });
        });
    }
}