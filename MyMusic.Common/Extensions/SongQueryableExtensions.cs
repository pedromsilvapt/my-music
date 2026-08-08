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
    /// i.e. songs they own <em>or</em> songs that have been shared with them via <see cref="SongSharing"/>.
    /// Used by single-song read endpoints (Get, Download, GetDevices, GetFilterValues, AutocompleteSongs).
    /// </summary>
    /// <remarks>
    /// Only the Song-scoped shared-access predicate is generalized here. The Album/Artist/Genre
    /// shared-access predicates differ per entity (Album.Songs vs Artist.Songs.Song vs
    /// Genre.Songs.Song — through their respective join entities), so they are inlined in each
    /// controller rather than generalized, to keep the per-entity navigation paths explicit and
    /// avoid over-abstraction.
    /// </remarks>
    public static IQueryable<Song> WhereAccessibleBy(this IQueryable<Song> query, long userId) =>
        query.Where(s => s.OwnerId == userId || s.SongSharings.Any(ss => ss.UserId == userId));

    /// <summary>
    /// Filters <paramref name="query"/> to songs owned by <paramref name="ownerId"/> that have been
    /// shared with <paramref name="currentUserId"/> — the "shared with me by this specific user" view.
    /// Used by <c>ListSongs</c> when <c>ownerId</c> is another user (gate-by-sharing semantics).
    /// </summary>
    public static IQueryable<Song> WhereOwnedOrSharedFromOwner(
        this IQueryable<Song> query,
        long currentUserId,
        long ownerId) =>
        query.Where(s => s.OwnerId == ownerId && s.SongSharings.Any(ss => ss.UserId == currentUserId));

}
