using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

    public DbSet<WishlistItem> WishlistItems { get; set; } = null!;

    public DbSet<DeviceSyncSession> DeviceSyncSessions { get; set; } = null!;

    public DbSet<DeviceSyncSessionRecord> DeviceSyncSessionRecords { get; set; } = null!;

    public DbSet<AuditNonConformity> AuditNonConformities { get; set; } = null!;

    public DbSet<AutoFetchedMetadata> AutoFetchedMetadata { get; set; } = null!;

    public DbSet<MetadataFetchTask> MetadataFetchTasks { get; set; } = null!;

    public DbSet<SongAcousticFingerprint> SongAcousticFingerprints { get; set; } = null!;

    public DbSet<ExcludedDuplicatePair> ExcludedDuplicatePairs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AutoFetchedMetadata entity configuration
        modelBuilder.Entity<AutoFetchedMetadata>(entity =>
        {
            // Value converter for SourceMetadata to support cross-database JSON storage
            var jsonConverter = new ValueConverter<JsonElement, string>(
                v => v.GetRawText(),
                v => string.IsNullOrEmpty(v) ? JsonDocument.Parse("{}").RootElement : JsonDocument.Parse(v).RootElement);
            entity.Property(e => e.SourceMetadata).HasConversion(jsonConverter);

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

        // AuditNonConformity entity configuration
        modelBuilder.Entity<AuditNonConformity>(entity =>
        {
            // Value converter for Data to support cross-database JSON storage
            // Works with both SQLite (tests) and PostgreSQL (production)
            var jsonConverter = new ValueConverter<JsonElement?, string>(
                v => v.HasValue ? v.Value.GetRawText() : "null",
                v => v == "null" ? null : JsonDocument.Parse(v).RootElement);
            entity.Property(e => e.Data).HasConversion(jsonConverter);

            // Index on SongId + AuditRuleId - uniqueness enforced in application code
            // (Partial index filter syntax differs between SQLite and PostgreSQL)
            entity.HasIndex(e => new { e.SongId, e.AuditRuleId });
        });

        // PlaylistSong entity configuration
        modelBuilder.Entity<PlaylistSong>(entity =>
        {
            // Composite index for efficient ordered queries within a playlist
            entity.HasIndex(e => new { e.PlaylistId, e.Order });
        });

        // Playlist entity configuration for CurrentSong relationship
        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.HasOne(e => e.CurrentSong)
                .WithMany()
                .HasForeignKey(e => e.CurrentSongId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // User entity configuration for CurrentQueue relationship
        // User has one optional CurrentQueue (Playlist), but Playlist can be owned by many users
        // This is a one-way navigation - User.CurrentQueue points to a Playlist
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasOne(e => e.CurrentQueue)
                .WithOne()
                .HasForeignKey<User>(e => e.CurrentQueueId);
        });

        // SongAcousticFingerprint entity configuration
        modelBuilder.Entity<SongAcousticFingerprint>(entity =>
        {
            entity.HasKey(e => new { e.Checksum, e.ChecksumAlgorithm, e.OwnerId });
            
            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId);

            entity.HasIndex(e => e.OwnerId);
        });

        // ExcludedDuplicatePair entity configuration
        modelBuilder.Entity<ExcludedDuplicatePair>(entity =>
        {
            entity.HasIndex(e => new { e.SongAId, e.SongBId, e.OwnerId }).IsUnique();

            entity.HasOne(e => e.SongA)
                .WithMany()
                .HasForeignKey(e => e.SongAId);

            entity.HasOne(e => e.SongB)
                .WithMany()
                .HasForeignKey(e => e.SongBId);

            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId);
        });
    }
}