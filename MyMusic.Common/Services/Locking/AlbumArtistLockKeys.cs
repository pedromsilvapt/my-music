namespace MyMusic.Common.Services;

/// <summary>
///     The advisory lock keys that protect artists and albums, which are found or created by name: the keys a song
///     import takes. Anything that creates, renames, merges or deletes them takes the keys of every name involved
///     (old and new) in a single <see cref="IAdvisoryLockService.AcquireTransactionLocksAsync"/> call.
/// </summary>
public static class AlbumArtistLockKeys
{
    /// <summary>
    ///     Creates the <see cref="AdvisoryLockScope.Artist"/> keys of <paramref name="artistNames"/> and of every
    ///     album artist, and the <see cref="AdvisoryLockScope.Album"/> keys of <paramref name="albums"/>.
    /// </summary>
    public static IReadOnlyList<AdvisoryLockKey> Create(long ownerId, IEnumerable<string> artistNames,
        IEnumerable<(string ArtistName, string AlbumName)> albums)
    {
        var albumList = albums.ToList();

        return artistNames
            .Concat(albumList.Select(album => album.ArtistName))
            .Distinct()
            .Select(name => AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, name))
            .Concat(albumList.Select(album =>
                AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, album.ArtistName, album.AlbumName)))
            .ToList();
    }
}
