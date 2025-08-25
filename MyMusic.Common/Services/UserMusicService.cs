using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public class UserMusicService(MusicDbContext db, long userId)
{
    public MusicDbContext Db { get; } = db;

    public long UserId { get; } = userId;

    /// <summary>
    /// Returns the song that matches the given checksum and checksum algorithm in the given repository
    /// </summary>
    /// <param name="checksum"></param>
    /// <param name="checksumAlgorithm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Song?> GetSongByChecksum(string checksum, string checksumAlgorithm, CancellationToken cancellationToken = default)
    {
        return await Db.Songs.FirstOrDefaultAsync(s => s.Owner.Id == UserId && s.Checksum == checksum && s.ChecksumAlgorithm == checksumAlgorithm, cancellationToken);
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
    /// Return all artists found on the database with the given name
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<Artist>> GetArtists(string name, CancellationToken cancellationToken = default)
    {
        return await Db.Artists.Where(a => a.Owner.Id == UserId && a.Name == name).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Return all albums found on the database with the given album name
    /// </summary>
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