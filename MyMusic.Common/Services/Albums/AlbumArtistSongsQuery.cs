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
        db.Songs.Where(s => s.OwnerId == ownerId && s.AlbumId == albumId);

    /// <summary>The songs the artist performs, together with the songs of the artist's albums.</summary>
    public static IQueryable<Song> OfArtist(MusicDbContext db, long ownerId, long artistId) =>
        db.Songs.Where(s => s.OwnerId == ownerId
                            && (s.Artists.Any(sa => sa.ArtistId == artistId) || s.Album.ArtistId == artistId));
}
