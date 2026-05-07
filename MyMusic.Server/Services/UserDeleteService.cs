using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;

namespace MyMusic.Server.Services;

public class UserDeleteService(
    MusicDbContext db,
    IFileSystem fileSystem,
    IOptions<Config> config,
    ILogger<UserDeleteService> logger) : IUserDeleteService
{
    public async Task<User?> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user == null)
        {
            logger.LogWarning("User {UserId} not found for deletion", id);
            return null;
        }

        var username = user.Username;
        logger.LogInformation("Deleting user {UserId} ({Username})", id, username);

        await DeleteDeviceSyncSessionRecordsAsync(id, cancellationToken);
        await DeleteDeviceSyncSessionsAsync(id, cancellationToken);
        await DeleteSongDevicesAsync(id, cancellationToken);
        await DeleteSongArtistsAsync(id, cancellationToken);
        await DeleteSongGenresAsync(id, cancellationToken);
        await DeleteSongSourcesAsync(id, cancellationToken);
        await DeletePlaylistSongsAsync(id, cancellationToken);
        await DeleteAutoFetchedMetadataAsync(id, cancellationToken);
        await DeleteMetadataFetchTasksAsync(id, cancellationToken);
        await DeleteExcludedDuplicatePairsAsync(id, cancellationToken);
        await DeleteSongAcousticFingerprintsAsync(id, cancellationToken);
        await DeleteAuditNonConformitiesAsync(id, cancellationToken);
        await DeletePlayHistoriesAsync(id, cancellationToken);
        await DeleteWishlistItemsAsync(id, cancellationToken);
        await DeletePurchasedSongsAsync(id, cancellationToken);
        await DeleteSongsWithArtworksAsync(id, cancellationToken);
        await DeleteAlbumsWithArtworksAsync(id, cancellationToken);
        await DeleteArtistsWithArtworksAsync(id, cancellationToken);
        await DeleteGenresAsync(id, cancellationToken);
        await DeleteDevicesAsync(id, cancellationToken);

        user.CurrentQueueId = null;
        await db.SaveChangesAsync(cancellationToken);

        await DeletePlaylistsAsync(id, cancellationToken);

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        var musicRepositoryPath = config.Value.MusicRepositoryPath;
        var userMusicDir = fileSystem.Path.Combine(musicRepositoryPath, username);
        if (fileSystem.Directory.Exists(userMusicDir))
        {
            logger.LogInformation("Deleting user music directory: {Path}", userMusicDir);
            fileSystem.Directory.Delete(userMusicDir, recursive: true);
        }
        else
        {
            logger.LogInformation("User music directory does not exist: {Path}", userMusicDir);
        }

        logger.LogInformation("User {UserId} ({Username}) deleted successfully", id, username);
        return user;
    }

    private async Task DeleteDeviceSyncSessionRecordsAsync(long ownerId, CancellationToken ct)
    {
        var records = await db.DeviceSyncSessionRecords
            .Where(r => r.Session.Device.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} DeviceSyncSessionRecords for user {UserId}", records, ownerId);
    }

    private async Task DeleteDeviceSyncSessionsAsync(long ownerId, CancellationToken ct)
    {
        var sessions = await db.DeviceSyncSessions
            .Where(s => s.Device.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} DeviceSyncSessions for user {UserId}", sessions, ownerId);
    }

    private async Task DeleteSongDevicesAsync(long ownerId, CancellationToken ct)
    {
        var songDevices = await db.SongDevices
            .Where(sd => sd.Device.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} SongDevices for user {UserId}", songDevices, ownerId);
    }

    private async Task DeleteSongArtistsAsync(long ownerId, CancellationToken ct)
    {
        var songArtists = await db.SongArtists
            .Where(sa => sa.Song.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} SongArtists for user {UserId}", songArtists, ownerId);
    }

    private async Task DeleteSongGenresAsync(long ownerId, CancellationToken ct)
    {
        var songGenres = await db.SongGenres
            .Where(sg => sg.Song.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} SongGenres for user {UserId}", songGenres, ownerId);
    }

    private async Task DeleteSongSourcesAsync(long ownerId, CancellationToken ct)
    {
        var songSources = await db.SongSources
            .Where(ss => ss.Song.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} SongSources for user {UserId}", songSources, ownerId);
    }

    private async Task DeletePlaylistSongsAsync(long ownerId, CancellationToken ct)
    {
        var playlistSongs = await db.PlaylistSongs
            .Where(ps => ps.Playlist.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} PlaylistSongs for user {UserId}", playlistSongs, ownerId);
    }

    private async Task DeleteAutoFetchedMetadataAsync(long ownerId, CancellationToken ct)
    {
        var metadata = await db.AutoFetchedMetadata
            .Where(m => m.Song.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} AutoFetchedMetadata for user {UserId}", metadata, ownerId);
    }

    private async Task DeleteMetadataFetchTasksAsync(long ownerId, CancellationToken ct)
    {
        var tasks = await db.MetadataFetchTasks
            .Where(t => t.Song.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} MetadataFetchTasks for user {UserId}", tasks, ownerId);
    }

    private async Task DeleteExcludedDuplicatePairsAsync(long ownerId, CancellationToken ct)
    {
        var pairs = await db.ExcludedDuplicatePairs
            .Where(p => p.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} ExcludedDuplicatePairs for user {UserId}", pairs, ownerId);
    }

    private async Task DeleteSongAcousticFingerprintsAsync(long ownerId, CancellationToken ct)
    {
        var fingerprints = await db.SongAcousticFingerprints
            .Where(f => f.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} SongAcousticFingerprints for user {UserId}", fingerprints, ownerId);
    }

    private async Task DeleteAuditNonConformitiesAsync(long ownerId, CancellationToken ct)
    {
        var nonConformities = await db.AuditNonConformities
            .Where(a => a.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} AuditNonConformities for user {UserId}", nonConformities, ownerId);
    }

    private async Task DeletePlayHistoriesAsync(long ownerId, CancellationToken ct)
    {
        var histories = await db.PlayHistories
            .Where(h => h.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} PlayHistories for user {UserId}", histories, ownerId);
    }

    private async Task DeleteWishlistItemsAsync(long ownerId, CancellationToken ct)
    {
        var items = await db.WishlistItems
            .Where(w => w.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} WishlistItems for user {UserId}", items, ownerId);
    }

    private async Task DeletePurchasedSongsAsync(long userId, CancellationToken ct)
    {
        var songs = await db.PurchasedSongs
            .Where(p => p.UserId == userId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} PurchasedSongs for user {UserId}", songs, userId);
    }

    private async Task DeleteSongsWithArtworksAsync(long ownerId, CancellationToken ct)
    {
        var artworkIds = await db.Songs
            .Where(s => s.OwnerId == ownerId && s.CoverId != null)
            .Select(s => s.CoverId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var songs = await db.Songs
            .Where(s => s.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} Songs for user {UserId}", songs, ownerId);

        if (artworkIds.Count != 0)
        {
            var deletedArtworks = await db.Artworks
                .Where(a => artworkIds.Contains(a.Id) &&
                    !db.Songs.Any(s => s.CoverId == a.Id) &&
                    !db.Albums.Any(al => al.CoverId == a.Id) &&
                    !db.Artists.Any(ar => ar.PhotoId == a.Id || ar.BackgroundId == a.Id))
                .ExecuteDeleteAsync(ct);
            logger.LogDebug("Deleted {Count} orphaned Song Artworks for user {UserId}", deletedArtworks, ownerId);
        }
    }

    private async Task DeleteAlbumsWithArtworksAsync(long ownerId, CancellationToken ct)
    {
        var artworkIds = await db.Albums
            .Where(a => a.OwnerId == ownerId && a.CoverId != null)
            .Select(a => a.CoverId!.Value)
            .Distinct()
            .ToListAsync(ct);

        await db.AlbumSources
            .Where(a => a.Album.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted AlbumSources for user {UserId}", ownerId);

        var albums = await db.Albums
            .Where(a => a.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} Albums for user {UserId}", albums, ownerId);

        if (artworkIds.Count != 0)
        {
            var deletedArtworks = await db.Artworks
                .Where(a => artworkIds.Contains(a.Id) &&
                    !db.Songs.Any(s => s.CoverId == a.Id) &&
                    !db.Albums.Any(al => al.CoverId == a.Id) &&
                    !db.Artists.Any(ar => ar.PhotoId == a.Id || ar.BackgroundId == a.Id))
                .ExecuteDeleteAsync(ct);
            logger.LogDebug("Deleted {Count} orphaned Album Artworks for user {UserId}", deletedArtworks, ownerId);
        }
    }

    private async Task DeleteArtistsWithArtworksAsync(long ownerId, CancellationToken ct)
    {
        var artistArtworks = await db.Artists
            .Where(a => a.OwnerId == ownerId && (a.PhotoId != null || a.BackgroundId != null))
            .Select(a => new { a.PhotoId, a.BackgroundId })
            .ToListAsync(ct);

        var artworkIds = artistArtworks
            .SelectMany(a => new long?[] { a.PhotoId, a.BackgroundId })
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        await db.ArtistSources
            .Where(a => a.Artist.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted ArtistSources for user {UserId}", ownerId);

        var artists = await db.Artists
            .Where(a => a.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} Artists for user {UserId}", artists, ownerId);

        if (artworkIds.Count != 0)
        {
            var deletedArtworks = await db.Artworks
                .Where(a => artworkIds.Contains(a.Id) &&
                    !db.Songs.Any(s => s.CoverId == a.Id) &&
                    !db.Albums.Any(al => al.CoverId == a.Id) &&
                    !db.Artists.Any(ar => ar.PhotoId == a.Id || ar.BackgroundId == a.Id))
                .ExecuteDeleteAsync(ct);
            logger.LogDebug("Deleted {Count} orphaned Artist Artworks for user {UserId}", deletedArtworks, ownerId);
        }
    }

    private async Task DeleteGenresAsync(long ownerId, CancellationToken ct)
    {
        var genres = await db.Genres
            .Where(g => g.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} Genres for user {UserId}", genres, ownerId);
    }

    private async Task DeleteDevicesAsync(long ownerId, CancellationToken ct)
    {
        var devices = await db.Devices
            .Where(d => d.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} Devices for user {UserId}", devices, ownerId);
    }

    private async Task DeletePlaylistsAsync(long ownerId, CancellationToken ct)
    {
        var playlists = await db.Playlists
            .Where(p => p.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);
        logger.LogDebug("Deleted {Count} Playlists for user {UserId}", playlists, ownerId);
    }
}
