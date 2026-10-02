using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Walks a song's state back through its recorded history: applying a revision's delta in reverse restores the
/// previous value of every field that revision changed, leaving the other fields untouched.
/// </summary>
public static class SongHistoryDeltaReverter
{
    /// <summary>
    /// Returns <paramref name="snapshot"/> as it was before the change recorded by <paramref name="delta"/>.
    /// </summary>
    public static SongSnapshot Revert(SongSnapshot snapshot, SongHistoryDelta delta)
    {
        var reverted = RevertFields(snapshot, delta);

        // A cover change may be recorded only as a cover_id change (the queue snapshot did not carry the cover
        // object), so never pair the restored cover id with the cover of a different artwork
        return reverted.Cover is { } cover && cover.Id != reverted.CoverId
            ? reverted with { Cover = null }
            : reverted;
    }

    private static SongSnapshot RevertFields(SongSnapshot snapshot, SongHistoryDelta delta) => snapshot with
    {
        Title = delta.Title is { } title ? title.Old! : snapshot.Title,
        Label = delta.Label is { } label ? label.Old! : snapshot.Label,
        AlbumId = delta.AlbumId?.Old ?? snapshot.AlbumId,
        CoverId = delta.CoverId is { } coverId ? coverId.Old : snapshot.CoverId,
        Year = delta.Year is { } year ? year.Old : snapshot.Year,
        Lyrics = delta.Lyrics is { } lyrics ? lyrics.Old : snapshot.Lyrics,
        Explicit = delta.Explicit?.Old ?? snapshot.Explicit,
        Size = delta.Size?.Old ?? snapshot.Size,
        Track = delta.Track is { } track ? track.Old : snapshot.Track,
        Duration = delta.Duration?.Old ?? snapshot.Duration,
        Bitrate = delta.Bitrate is { } bitrate ? bitrate.Old : snapshot.Bitrate,
        OwnerId = delta.OwnerId?.Old ?? snapshot.OwnerId,
        Rating = delta.Rating is { } rating ? rating.Old : snapshot.Rating,
        IsFavorite = delta.IsFavorite?.Old ?? snapshot.IsFavorite,
        RepositoryPath = delta.RepositoryPath is { } repositoryPath ? repositoryPath.Old! : snapshot.RepositoryPath,
        Checksum = delta.Checksum is { } checksum ? checksum.Old! : snapshot.Checksum,
        ChecksumAlgorithm = delta.ChecksumAlgorithm is { } algorithm ? algorithm.Old! : snapshot.ChecksumAlgorithm,
        AddedAt = delta.AddedAt is { } addedAt ? addedAt.Old : snapshot.AddedAt,
        CreatedAt = delta.CreatedAt?.Old ?? snapshot.CreatedAt,
        ModifiedAt = delta.ModifiedAt?.Old ?? snapshot.ModifiedAt,
        FileModifiedAt = delta.FileModifiedAt is { } fileModifiedAt ? fileModifiedAt.Old : snapshot.FileModifiedAt,
        Album = delta.Album is { } album ? album.Old : snapshot.Album,
        Artists = delta.Artists is { } artists ? artists.Old ?? [] : snapshot.Artists,
        Genres = delta.Genres is { } genres ? genres.Old ?? [] : snapshot.Genres,
        Sources = delta.Sources is { } sources ? sources.Old ?? [] : snapshot.Sources,
        Devices = delta.Devices is { } devices ? devices.Old ?? [] : snapshot.Devices,
        Cover = delta.Cover is { } cover ? cover.Old : snapshot.Cover,
    };
}
