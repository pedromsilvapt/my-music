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
        string songNavigationPath)
        where TEntity : class =>
        query
            .Include($"{songNavigationPath}.Album.Artist")
            .Include($"{songNavigationPath}.Artists.Artist")
            .Include($"{songNavigationPath}.Genres.Genre")
            .Include($"{songNavigationPath}.Devices.Device");
}
