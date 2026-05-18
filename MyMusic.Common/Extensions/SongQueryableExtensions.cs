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
}
