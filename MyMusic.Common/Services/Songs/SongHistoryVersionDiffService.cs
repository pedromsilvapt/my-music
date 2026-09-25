using Microsoft.EntityFrameworkCore;

using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory.Models;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Builds the field-by-field metadata diff shown by the version diff viewer
/// for a selected <see cref="SongHistory"/> revision. Unlike the worker's
/// delta computation (which compares two live snapshots), this service simply
/// surfaces the stored <see cref="SongHistoryDelta"/> of the selected
/// revision: every field the delta carries becomes a populated entry with
/// its <c>Old</c>/<c>New</c> values, and every field absent from the delta is
/// omitted entirely. This means the viewer only shows fields that actually
/// changed in that revision. Ownership is verified via the song's
/// <see cref="Song.OwnerId"/> against <see cref="ICurrentUser"/>.
/// </summary>
public class SongHistoryVersionDiffService(
    MusicDbContext db,
    ICurrentUser currentUser) : ISongHistoryVersionDiffService
{
    /// <inheritdoc />
    public async Task<SongHistoryVersionDiffResult?> GetVersionDiffAsync(
        long songId,
        long historyId,
        CancellationToken cancellationToken = default)
    {
        // Ownership check: only compute diffs for songs that exist and belong to the
        // current user. If the song has been deleted, we can no longer verify ownership.
        var owned = await db.Songs
            .AnyAsync(s => s.Id == songId && s.OwnerId == currentUser.Id, cancellationToken);

        if (!owned)
        {
            return null;
        }

        // Fetch the selected history entry (scoped to the route's songId to prevent
        // cross-song history id confusion).
        var selected = await db.SongHistories
            .Where(h => h.Id == historyId && h.SongId == songId)
            .FirstOrDefaultAsync(cancellationToken);

        if (selected == null)
        {
            return null;
        }

        // Fetch the immediate predecessor row (if any) for the old revision metadata.
        // The first revision has no predecessor, so OldRevision/OldVersionDate are null.
        var predecessor = await db.SongHistories
            .Where(h => h.SongId == songId && h.SongRevision < selected.SongRevision)
            .OrderByDescending(h => h.SongRevision)
            .Select(h => new { h.SongRevision, h.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        // Project the stored delta directly into the viewer-facing model. Only fields
        // carried by the delta (non-null FieldChange<T>) become populated entries;
        // absent fields stay null and are omitted from the JSON response.
        var metadata = MapDelta(selected.Diff);

        return new SongHistoryVersionDiffResult
        {
            Metadata = metadata,
            OldVersionDate = predecessor?.CreatedAt,
            NewVersionDate = selected.CreatedAt,
            OldRevision = predecessor?.SongRevision,
            NewRevision = selected.SongRevision,
        };
    }

    // ---------------------------------------------------------------------
    // Delta → model mapping. Each non-null FieldChange<T> on the delta is
    // projected into a SongHistoryVersionField<T>; absent fields are left
    // null so the JSON serializer omits them (WhenWritingNull).
    // ---------------------------------------------------------------------

    private static SongHistoryVersionDiffModel MapDelta(SongHistoryDelta delta) => new()
    {
        Title = MapScalar(delta.Title),
        Label = MapScalar(delta.Label),
        AlbumId = MapScalar(delta.AlbumId),
        CoverId = MapScalar(delta.CoverId),
        Year = MapScalar(delta.Year),
        Lyrics = MapScalar(delta.Lyrics),
        Explicit = MapScalar(delta.Explicit),
        Size = MapScalar(delta.Size),
        Track = MapScalar(delta.Track),
        Duration = MapDuration(delta.Duration),
        Bitrate = MapScalar(delta.Bitrate),
        OwnerId = MapScalar(delta.OwnerId),
        Rating = MapScalar(delta.Rating),
        IsFavorite = MapScalar(delta.IsFavorite),
        PlayCount = MapScalar(delta.PlayCount),
        RepositoryPath = MapScalar(delta.RepositoryPath),
        Checksum = MapScalar(delta.Checksum),
        ChecksumAlgorithm = MapScalar(delta.ChecksumAlgorithm),
        AddedAt = MapScalar(delta.AddedAt),
        CreatedAt = MapScalar(delta.CreatedAt),
        ModifiedAt = MapScalar(delta.ModifiedAt),
        FileModifiedAt = MapScalar(delta.FileModifiedAt),
        Cover = MapCover(delta.Cover),
        Album = MapAlbum(delta.Album),
        AlbumArtist = MapAlbumArtist(delta.Album),
        Artists = MapArtists(delta.Artists),
        Genres = MapGenres(delta.Genres),
        Sources = MapSources(delta.Sources),
        Devices = MapDevices(delta.Devices),
    };

    private static SongHistoryVersionField<T?>? MapScalar<T>(FieldChange<T>? change)
        => change is null ? null : new() { Old = change.Old, New = change.New };

    private static SongHistoryVersionField<string?>? MapDuration(FieldChange<TimeSpan>? change)
        => change is null ? null : new()
        {
            Old = change.Old == default ? null : change.Old.ToString(@"hh\:mm\:ss"),
            New = change.New == default ? null : change.New.ToString(@"hh\:mm\:ss"),
        };

    private static SongHistoryVersionField<string?>? MapCover(FieldChange<SongSnapshotCover?>? change)
        => change is null ? null : new()
        {
            Old = GetCoverUrl(change.Old),
            New = GetCoverUrl(change.New),
        };

    private static SongHistoryVersionField<SongHistoryVersionAlbum?>? MapAlbum(FieldChange<SongSnapshotAlbum?>? change)
        => change is null ? null : new()
        {
            Old = GetAlbum(change.Old),
            New = GetAlbum(change.New),
        };

    /// <summary>
    /// The album artist is derived from the album delta's <c>ArtistName</c>.
    /// Returns <c>null</c> when the album didn't change or when neither old nor
    /// new artist name is present (e.g. old-shape rows without artist fields),
    /// so consumers see a clean "no artist change" signal.
    /// </summary>
    private static SongHistoryVersionField<string?>? MapAlbumArtist(FieldChange<SongSnapshotAlbum?>? albumChange)
    {
        if (albumChange is null)
            return null;

        var oldName = albumChange.Old?.ArtistName;
        var newName = albumChange.New?.ArtistName;

        if (oldName is null && newName is null)
            return null;

        return new()
        {
            Old = oldName,
            New = newName,
        };
    }

    private static SongHistoryVersionField<List<SongHistoryVersionArtist>?>? MapArtists(
        FieldChange<List<SongSnapshotArtist>>? change)
        => change is null ? null : new()
        {
            Old = GetArtists(change.Old),
            New = GetArtists(change.New),
        };

    private static SongHistoryVersionField<List<string>?>? MapGenres(
        FieldChange<List<SongSnapshotGenre>>? change)
        => change is null ? null : new()
        {
            Old = GetGenres(change.Old),
            New = GetGenres(change.New),
        };

    private static SongHistoryVersionField<List<SongHistoryVersionSource>?>? MapSources(
        FieldChange<List<SongSnapshotSource>>? change)
        => change is null ? null : new()
        {
            Old = GetSources(change.Old),
            New = GetSources(change.New),
        };

    private static SongHistoryVersionField<List<SongHistoryVersionDevice>?>? MapDevices(
        FieldChange<List<SongSnapshotDevice>>? change)
        => change is null ? null : new()
        {
            Old = GetDevices(change.Old),
            New = GetDevices(change.New),
        };

    // ---------------------------------------------------------------------
    // Field mappers — each converts a snapshot sub-object into the
    // viewer-facing DTO shape. Null snapshots/sub-objects yield null.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Returns the cover as a <c>data:image/{mime};base64,{data}</c> URL, or <c>null</c>
    /// when the snapshot has no cover object or no base64 data.
    /// </summary>
    private static string? GetCoverUrl(SongSnapshotCover? cover)
    {
        if (cover is null || string.IsNullOrEmpty(cover.Data))
        {
            return null;
        }

        var mime = string.IsNullOrEmpty(cover.MimeType) ? "image/jpeg" : cover.MimeType;
        return $"data:{mime};base64,{cover.Data}";
    }

    /// <summary>
    /// Returns the album as a <see cref="SongHistoryVersionAlbum"/>. The snapshot stores
    /// the album name under the <c>title</c> key (mapped to <see cref="SongHistoryVersionAlbum.Name"/>).
    /// The album artist is derived from the snapshot's <c>ArtistName</c>.
    /// </summary>
    private static SongHistoryVersionAlbum? GetAlbum(SongSnapshotAlbum? album)
    {
        if (album is null || string.IsNullOrEmpty(album.Title))
        {
            return null;
        }

        return new SongHistoryVersionAlbum
        {
            Name = album.Title,
            ArtistName = album.ArtistName,
        };
    }

    private static List<SongHistoryVersionArtist>? GetArtists(List<SongSnapshotArtist>? artists)
    {
        if (artists is null)
        {
            return null;
        }

        return artists
            .Select(a => new SongHistoryVersionArtist { Name = a.Name })
            .ToList();
    }

    private static List<string>? GetGenres(List<SongSnapshotGenre>? genres)
    {
        if (genres is null)
        {
            return null;
        }

        return genres.Select(g => g.Name).ToList();
    }

    private static List<SongHistoryVersionSource>? GetSources(List<SongSnapshotSource>? sources)
    {
        if (sources is null)
        {
            return null;
        }

        return sources
            .Select(s => new SongHistoryVersionSource { Name = s.Name })
            .ToList();
    }

    private static List<SongHistoryVersionDevice>? GetDevices(List<SongSnapshotDevice>? devices)
    {
        if (devices is null)
        {
            return null;
        }

        return devices
            .Select(d => new SongHistoryVersionDevice
            {
                DevicePath = d.DevicePath,
                SyncAction = d.SyncAction?.ToString(),
            })
            .ToList();
    }
}