using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Extensions;

public static class SongQueryableExtensions
{
    public static IQueryable<Song> IncludeSongMetadata(this IQueryable<Song> query) =>
        query
            .Include(s => s.Album)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Artists)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Genres)
            .ThenInclude(g => g.Genre)
            .Include(s => s.Devices)
            .ThenInclude(sd => sd.Device);

    public static IQueryable<TEntity> IncludeSongMetadata<TEntity>(
        this IQueryable<TEntity> query,
        string songNavigationPath,
        bool includeAlbum = true)
        where TEntity : class
    {
        var result = query
            .Include($"{songNavigationPath}.Artists.Artist")
            .Include($"{songNavigationPath}.Genres.Genre")
            .Include($"{songNavigationPath}.Devices.Device");

        if (includeAlbum)
        {
            result = result.Include($"{songNavigationPath}.Album.Artist");
        }

        return result;
    }

    /// <summary>
    /// Filters <paramref name="query"/> to songs the given <paramref name="userId"/> can read —
    /// i.e. songs they own <em>or</em> songs shared with them through a shared playlist (see <see cref="Song.IsSharedWith"/>).
    /// Used by single-song read endpoints (Get, Download, GetDevices, GetFilterValues, AutocompleteSongs).
    /// </summary>
    /// <remarks>
    /// The Album/Artist/Genre shared-access predicates navigate through different join entities
    /// (Album.Songs vs Artist.Songs.Song vs Genre.Songs.Song), so they are inlined in each
    /// controller, but all of them delegate the actual share check to <see cref="Song.IsSharedWith"/>.
    /// </remarks>
    public static IQueryable<Song> WhereAccessibleBy(this IQueryable<Song> query, long userId) =>
        query.Where(s => s.OwnerId == userId || s.IsSharedWith(userId));

    /// <summary>
    /// Filters <paramref name="query"/> to songs owned by <paramref name="ownerId"/> that have been
    /// shared with <paramref name="currentUserId"/> — the "shared with me by this specific user" view.
    /// Used by <c>ListSongs</c> when <c>ownerId</c> is another user (gate-by-sharing semantics).
    /// </summary>
    public static IQueryable<Song> WhereOwnedOrSharedFromOwner(
        this IQueryable<Song> query,
        long currentUserId,
        long ownerId) =>
        query.Where(s => s.OwnerId == ownerId && s.IsSharedWith(currentUserId));

}
