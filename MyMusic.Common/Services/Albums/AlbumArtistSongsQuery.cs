using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Albums;

/// <summary>
///     The songs an album or an artist operation (delete, rename, merge) affects. Shared by the previews, which count
///     them, and by the operations, which update them.
/// </summary>
public static class AlbumArtistSongsQuery
{
    /// <summary>The songs of the album.</summary>
    public static IQueryable<Song> OfAlbum(MusicDbContext db, long ownerId, long albumId) =>
        OfAlbums(db, ownerId, [albumId]);

    /// <summary>The songs of any of the albums.</summary>
    public static IQueryable<Song> OfAlbums(MusicDbContext db, long ownerId, IReadOnlyCollection<long> albumIds) =>
        db.Songs.Where(s => s.OwnerId == ownerId && albumIds.Contains(s.AlbumId));

    /// <summary>The songs the artist performs, together with the songs of the artist's albums.</summary>
    public static IQueryable<Song> OfArtist(MusicDbContext db, long ownerId, long artistId) =>
        OfArtists(db, ownerId, [artistId]);

    /// <summary>
    ///     The songs any of the artists performs, together with the songs of their albums. A song several of them
    ///     are involved in is returned once.
    /// </summary>
    public static IQueryable<Song> OfArtists(MusicDbContext db, long ownerId, IReadOnlyCollection<long> artistIds) =>
        db.Songs.Where(s => s.OwnerId == ownerId
                            && (s.Artists.Any(sa => artistIds.Contains(sa.ArtistId))
                                || artistIds.Contains(s.Album.ArtistId)));
}
