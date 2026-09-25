using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Services.SongHistory;

public class SongHistoryDiffService : ISongHistoryDiffService
{
    public SongHistoryDelta ComputeDiff(SongSnapshot oldSnapshot, SongSnapshot? newSnapshot)
    {
        if (newSnapshot is null)
        {
            return new SongHistoryDelta { Action = "deleted" };
        }

        return new SongHistoryDelta
        {
            Title = DiffScalar(oldSnapshot.Title, newSnapshot.Title),
            Label = DiffScalar(oldSnapshot.Label, newSnapshot.Label),
            AlbumId = DiffScalar(oldSnapshot.AlbumId, newSnapshot.AlbumId),
            CoverId = DiffScalar(oldSnapshot.CoverId, newSnapshot.CoverId),
            Year = DiffScalar(oldSnapshot.Year, newSnapshot.Year),
            Lyrics = DiffScalar(oldSnapshot.Lyrics, newSnapshot.Lyrics),
            Explicit = DiffScalar(oldSnapshot.Explicit, newSnapshot.Explicit),
            Size = DiffScalar(oldSnapshot.Size, newSnapshot.Size),
            Track = DiffScalar(oldSnapshot.Track, newSnapshot.Track),
            Duration = DiffScalar(oldSnapshot.Duration, newSnapshot.Duration),
            Bitrate = DiffScalar(oldSnapshot.Bitrate, newSnapshot.Bitrate),
            OwnerId = DiffScalar(oldSnapshot.OwnerId, newSnapshot.OwnerId),
            Rating = DiffScalar(oldSnapshot.Rating, newSnapshot.Rating),
            IsFavorite = DiffScalar(oldSnapshot.IsFavorite, newSnapshot.IsFavorite),
            PlayCount = DiffScalar(oldSnapshot.PlayCount, newSnapshot.PlayCount),
            RepositoryPath = DiffScalar(oldSnapshot.RepositoryPath, newSnapshot.RepositoryPath),
            Checksum = DiffScalar(oldSnapshot.Checksum, newSnapshot.Checksum),
            ChecksumAlgorithm = DiffScalar(oldSnapshot.ChecksumAlgorithm, newSnapshot.ChecksumAlgorithm),
            AddedAt = DiffScalar(oldSnapshot.AddedAt, newSnapshot.AddedAt),
            CreatedAt = DiffScalar(oldSnapshot.CreatedAt, newSnapshot.CreatedAt),
            ModifiedAt = DiffScalar(oldSnapshot.ModifiedAt, newSnapshot.ModifiedAt),
            FileModifiedAt = DiffScalar(oldSnapshot.FileModifiedAt, newSnapshot.FileModifiedAt),
            Album = DiffAlbum(oldSnapshot.Album, newSnapshot.Album),
            Artists = DiffList(oldSnapshot.Artists, newSnapshot.Artists, a => (a.Id, a.Name)),
            Genres = DiffList(oldSnapshot.Genres, newSnapshot.Genres, g => (g.Id, g.Name)),
            Sources = DiffList(oldSnapshot.Sources, newSnapshot.Sources, s => (s.Id, s.Name)),
            Devices = DiffList(oldSnapshot.Devices, newSnapshot.Devices, d => (d.Id, d.DevicePath, d.SyncAction)),
            Cover = DiffCover(oldSnapshot.Cover, newSnapshot.Cover, oldSnapshot.CoverId, newSnapshot.CoverId),
        };
    }

    private static FieldChange<T>? DiffScalar<T>(T oldVal, T newVal) =>
        EqualityComparer<T>.Default.Equals(oldVal, newVal)
            ? null
            : new FieldChange<T> { Old = oldVal, New = newVal };

    private static FieldChange<SongSnapshotAlbum?>? DiffAlbum(SongSnapshotAlbum? oldAlbum, SongSnapshotAlbum? newAlbum)
    {
        if (oldAlbum is null && newAlbum is null) return null;
        if (oldAlbum is null || newAlbum is null) return new FieldChange<SongSnapshotAlbum?> { Old = oldAlbum, New = newAlbum };
        if (oldAlbum.Id != newAlbum.Id || oldAlbum.Title != newAlbum.Title
            || oldAlbum.ArtistId != newAlbum.ArtistId || oldAlbum.ArtistName != newAlbum.ArtistName)
            return new FieldChange<SongSnapshotAlbum?> { Old = oldAlbum, New = newAlbum };
        return null;
    }

    private static FieldChange<List<T>>? DiffList<T, TKey>(List<T> oldList, List<T> newList, Func<T, TKey> keySelector)
    {
        var oldKeys = oldList.Select(keySelector).OrderBy(k => k);
        var newKeys = newList.Select(keySelector).OrderBy(k => k);
        return oldKeys.SequenceEqual(newKeys)
            ? null
            : new FieldChange<List<T>> { Old = oldList, New = newList };
    }

    /// <summary>
    /// Computes the cover field change, accounting for the trigger's conditional
    /// cover inclusion. The <c>song_history</c> trigger only embeds the cover
    /// object in the queue snapshot when <c>cover_id</c> actually changed, so a
    /// snapshot whose <c>Cover</c> is <c>null</c> may simply reflect that the
    /// cover was not re-captured (not that the cover was removed). When both
    /// snapshots share the same non-null <c>CoverId</c>, the cover is unchanged
    /// even if one side's <c>Cover</c> object is absent, so the diff is
    /// suppressed. Real cover add/remove/replace events always coincide with a
    /// <c>CoverId</c> change and are reported as before.
    /// </summary>
    private static FieldChange<SongSnapshotCover?>? DiffCover(
        SongSnapshotCover? oldCover,
        SongSnapshotCover? newCover,
        long? oldCoverId,
        long? newCoverId)
    {
        // Same non-null cover id ⇒ the cover is unchanged regardless of whether
        // the trigger included the cover object on either side.
        if (oldCoverId is not null && oldCoverId == newCoverId)
        {
            return null;
        }

        if (oldCover is null && newCover is null) return null;
        if (oldCover is null || newCover is null) return new FieldChange<SongSnapshotCover?> { Old = oldCover, New = newCover };
        if (oldCover.Id != newCover.Id
            || oldCover.MimeType != newCover.MimeType
            || oldCover.Width != newCover.Width
            || oldCover.Height != newCover.Height
            || oldCover.Data != newCover.Data)
            return new FieldChange<SongSnapshotCover?> { Old = oldCover, New = newCover };
        return null;
    }
}