using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public class UserMusicService(MusicDbContext db, long userId)
{
    public MusicDbContext Db { get; } = db;

    public long UserId { get; } = userId;

    /// <summary>
    /// Returns the song whose file has the given checksum, either now or in a previous version (see
    /// <see cref="FindSongsByChecksums"/>). When the returned song's <see cref="Song.Checksum"/> differs from
    /// <paramref name="checksum"/>, the checksum belongs to an older version of its file.
    /// </summary>
    /// <param name="checksum"></param>
    /// <param name="checksumAlgorithm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Song?> GetSongByChecksum(string checksum, string checksumAlgorithm, CancellationToken cancellationToken = default)
    {
        var songs = await FindSongsByChecksums([checksum], checksumAlgorithm, cancellationToken);

        return songs.GetValueOrDefault(checksum);
    }

    /// <summary>
    /// Returns, for each of the given checksums that matches, the song whose file has that checksum. Songs whose
    /// current file matches take precedence; otherwise the song with the most recent previous version matching
    /// the checksum is returned (see <see cref="SongChecksum"/>). Checksums matching no song are left out.
    /// </summary>
    /// <param name="checksums"></param>
    /// <param name="checksumAlgorithm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Dictionary<string, Song>> FindSongsByChecksums(IReadOnlyCollection<string> checksums,
        string checksumAlgorithm, CancellationToken cancellationToken = default)
    {
        var songs = (await Db.Songs
                .Where(s => s.OwnerId == UserId && s.ChecksumAlgorithm == checksumAlgorithm && checksums.Contains(s.Checksum))
                .ToListAsync(cancellationToken))
            .GroupBy(s => s.Checksum)
            .ToDictionary(g => g.Key, g => g.First());

        var unmatched = checksums.Where(c => !songs.ContainsKey(c)).Distinct().ToList();
        if (unmatched.Count == 0)
        {
            return songs;
        }

        var previousVersions = await Db.SongChecksums
            .Where(sc => sc.Song.OwnerId == UserId && sc.ChecksumAlgorithm == checksumAlgorithm && unmatched.Contains(sc.Checksum))
            .OrderByDescending(sc => sc.CreatedAt)
            .Select(sc => new { sc.Checksum, sc.Song })
            .ToListAsync(cancellationToken);

        foreach (var previousVersion in previousVersions)
        {
            songs.TryAdd(previousVersion.Checksum, previousVersion.Song);
        }

        return songs;
    }

    /// <summary>
    /// Returns the song that matches the given repository path
    /// </summary>
    /// <param name="path"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Song?> GetSongByPath(string path, CancellationToken cancellationToken = default)
    {
        return await Db.Songs.FirstOrDefaultAsync(s => s.Owner.Id == UserId && s.RepositoryPath == path, cancellationToken);
    }

    /// <summary>
    /// Return the genre found on the database with the given name
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Genre?> GetGenre(string name, CancellationToken cancellationToken = default)
    {
        return await Db.Genres.FirstOrDefaultAsync(a => a.Owner.Id == UserId && a.Name == name, cancellationToken);
    }

    /// <summary>
    /// Returns the genre with the given name, creating it first if it does not exist yet. Relies on the unique
    /// (owner, name) index instead of locks, so concurrent imports of the same new genre never fail: a concurrent
    /// insert makes this one wait for that transaction to finish, and then do nothing.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Genre> UpsertGenre(string name, CancellationToken cancellationToken = default)
    {
        // Table and column names come from the EF model (never from user input), so this works with any naming convention
        var entityType = Db.Model.FindEntityType(typeof(Genre))!;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        var ownerIdColumn = entityType.FindProperty(nameof(Genre.OwnerId))!.GetColumnName(table)!;
        var nameColumn = entityType.FindProperty(nameof(Genre.Name))!.GetColumnName(table)!;

        var sql = $"INSERT INTO \"{table.Name}\" (\"{ownerIdColumn}\", \"{nameColumn}\") VALUES ({{0}}, {{1}}) " +
                  $"ON CONFLICT (\"{ownerIdColumn}\", \"{nameColumn}\") DO NOTHING";

        await Db.Database.ExecuteSqlRawAsync(sql, [UserId, name], cancellationToken);

        return await Db.Genres.FirstAsync(g => g.OwnerId == UserId && g.Name == name, cancellationToken);
    }

    /// <summary>
    /// Return all artists found on the database with the given name
    /// </summary>
    /// <remarks>
    /// Artist names are not unique: different real-world artists can share the same name, so
    /// multiple artists being returned is expected. During import, any of them is considered
    /// the most likely match (see the album/artist matching heuristic in <see cref="MusicService"/>).
    /// </remarks>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<Artist>> GetArtists(string name, CancellationToken cancellationToken = default)
    {
        return await Db.Artists.Where(a => a.Owner.Id == UserId && a.Name == name).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Return the first album found on the database with the given album name that belongs to
    /// any artist with the given artist name
    /// </summary>
    /// <remarks>
    /// Since artist names are not unique, this searches across every artist with the given name.
    /// An album with the same name, by an artist with the same name, is the strongest match signal
    /// available from file tags, so it is considered the most likely match during import.
    /// </remarks>
    /// <param name="artistName"></param>
    /// <param name="albumName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Album?> GetArtistAlbum(string artistName, string albumName, CancellationToken cancellationToken = default)
    {
        var artists = await GetArtists(artistName, cancellationToken);

        if (!artists.Any())
        {
            return null;
        }

        var album = await GetArtistAlbum(artists.Select(a => a.Id).ToList(), albumName, cancellationToken);

        // If we found an album belonging to one of the artists on the list, save the reference to that artist object
        if (album != null)
        {
            album.Artist = artists.First(a => a.Id == album.ArtistId);
        }

        return album;
    }

    /// <summary>
    /// Return the first album found on the database with the given album name that belongs to one of the artists on the list
    /// </summary>
    /// <param name="artistIds"></param>
    /// <param name="albumName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Album?> GetArtistAlbum(List<long> artistIds, string albumName, CancellationToken cancellationToken = default)
    {
        return await Db.Albums.FirstOrDefaultAsync(a => a.OwnerId == UserId && a.Name == albumName && artistIds.Contains(a.ArtistId), cancellationToken);
    }

    /// <summary>
    /// Return all albums found on the database with the given album name
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<Album>> GetAlbums(string name, CancellationToken cancellationToken = default)
    {
        return await Db.Albums.Where(a => a.OwnerId == UserId && a.Name == name).ToListAsync<Album>(cancellationToken);
    }
}