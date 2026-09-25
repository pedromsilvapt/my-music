using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.SongHistory.Models;
using Shouldly;

namespace MyMusic.Common.Tests.Services.SongHistory;

public class SongHistoryDiffServiceSpecs
{
    private readonly SongHistoryDiffService _service = new();

    private static SongSnapshot BuildSnapshot(
        string title = "Song",
        string label = "Label",
        long albumId = 1,
        long? coverId = null,
        int? year = null,
        string? lyrics = null,
        bool explicitFlag = false,
        long size = 0,
        int? track = null,
        TimeSpan? duration = null,
        int? bitrate = null,
        long ownerId = 1,
        decimal? rating = null,
        bool isFavorite = false,
        int playCount = 0,
        string repositoryPath = "/music/song.mp3",
        string checksum = "abc",
        string checksumAlgorithm = "XxHash128",
        DateTime? addedAt = null,
        DateTime? createdAt = null,
        DateTime? modifiedAt = null,
        DateTime? fileModifiedAt = null,
        string action = "updated",
        SongSnapshotAlbum? album = null,
        List<SongSnapshotArtist>? artists = null,
        List<SongSnapshotGenre>? genres = null,
        List<SongSnapshotSource>? sources = null,
        List<SongSnapshotDevice>? devices = null,
        SongSnapshotCover? cover = null)
    {
        var now = DateTime.UtcNow;
        return new SongSnapshot
        {
            Title = title,
            Label = label,
            AlbumId = albumId,
            CoverId = coverId,
            Year = year,
            Lyrics = lyrics,
            Explicit = explicitFlag,
            Size = size,
            Track = track,
            Duration = duration ?? TimeSpan.Zero,
            Bitrate = bitrate,
            OwnerId = ownerId,
            Rating = rating,
            IsFavorite = isFavorite,
            PlayCount = playCount,
            RepositoryPath = repositoryPath,
            Checksum = checksum,
            ChecksumAlgorithm = checksumAlgorithm,
            AddedAt = addedAt,
            CreatedAt = createdAt ?? now,
            ModifiedAt = modifiedAt ?? now,
            FileModifiedAt = fileModifiedAt,
            Action = action,
            Album = album,
            Artists = artists ?? [],
            Genres = genres ?? [],
            Sources = sources ?? [],
            Devices = devices ?? [],
            Cover = cover,
        };
    }

    [Fact]
    public void ComputeDiff_NoChanges_ReturnsDeltaWithAllNullFieldChanges()
    {
        var fixedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var oldSnapshot = BuildSnapshot(createdAt: fixedTime, modifiedAt: fixedTime, addedAt: fixedTime, fileModifiedAt: fixedTime);
        var newSnapshot = BuildSnapshot(createdAt: fixedTime, modifiedAt: fixedTime, addedAt: fixedTime, fileModifiedAt: fixedTime);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Action.ShouldBeNull();
        delta.Title.ShouldBeNull();
        delta.Label.ShouldBeNull();
        delta.AlbumId.ShouldBeNull();
        delta.CoverId.ShouldBeNull();
        delta.Year.ShouldBeNull();
        delta.Lyrics.ShouldBeNull();
        delta.Explicit.ShouldBeNull();
        delta.Size.ShouldBeNull();
        delta.Track.ShouldBeNull();
        delta.Duration.ShouldBeNull();
        delta.Bitrate.ShouldBeNull();
        delta.OwnerId.ShouldBeNull();
        delta.Rating.ShouldBeNull();
        delta.IsFavorite.ShouldBeNull();
        delta.PlayCount.ShouldBeNull();
        delta.RepositoryPath.ShouldBeNull();
        delta.Checksum.ShouldBeNull();
        delta.ChecksumAlgorithm.ShouldBeNull();
        delta.AddedAt.ShouldBeNull();
        delta.CreatedAt.ShouldBeNull();
        delta.ModifiedAt.ShouldBeNull();
        delta.FileModifiedAt.ShouldBeNull();
        delta.Album.ShouldBeNull();
        delta.Artists.ShouldBeNull();
        delta.Genres.ShouldBeNull();
        delta.Sources.ShouldBeNull();
        delta.Devices.ShouldBeNull();
        delta.Cover.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_SingleScalarChange_ReturnsOnlyThatFieldChange()
    {
        var oldSnapshot = BuildSnapshot(title: "Old Title");
        var newSnapshot = BuildSnapshot(title: "New Title");

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Title.ShouldNotBeNull();
        delta.Title.Old.ShouldBe("Old Title");
        delta.Title.New.ShouldBe("New Title");
        delta.Label.ShouldBeNull();
        delta.Year.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_MultipleScalarChanges_ReturnsOnlyChangedFields()
    {
        var oldSnapshot = BuildSnapshot(title: "Old", year: 2020, lyrics: "old");
        var newSnapshot = BuildSnapshot(title: "New", year: 2024, lyrics: "new");

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Title.ShouldNotBeNull();
        delta.Title.Old.ShouldBe("Old");
        delta.Title.New.ShouldBe("New");
        delta.Year.ShouldNotBeNull();
        delta.Year.Old.ShouldBe(2020);
        delta.Year.New.ShouldBe(2024);
        delta.Lyrics.ShouldNotBeNull();
        delta.Lyrics.Old.ShouldBe("old");
        delta.Lyrics.New.ShouldBe("new");
        delta.Label.ShouldBeNull();
        delta.AlbumId.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_ArtistsChanged_ReturnsArtistsFieldChange()
    {
        var oldArtists = new List<SongSnapshotArtist>
        {
            new() { Id = 1, Name = "Artist A" },
        };
        var newArtists = new List<SongSnapshotArtist>
        {
            new() { Id = 1, Name = "Artist A" },
            new() { Id = 2, Name = "Artist B" },
        };
        var oldSnapshot = BuildSnapshot(artists: oldArtists);
        var newSnapshot = BuildSnapshot(artists: newArtists);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Artists.ShouldNotBeNull();
        delta.Artists.Old.ShouldBe(oldArtists);
        delta.Artists.New.ShouldBe(newArtists);
        delta.Title.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_AlbumChanged_ReturnsAlbumFieldChange()
    {
        var oldAlbum = new SongSnapshotAlbum { Id = 1, Title = "Old Album", ArtistId = 10, ArtistName = "Old Artist" };
        var newAlbum = new SongSnapshotAlbum { Id = 2, Title = "New Album", ArtistId = 20, ArtistName = "New Artist" };
        var oldSnapshot = BuildSnapshot(album: oldAlbum, albumId: 1);
        var newSnapshot = BuildSnapshot(album: newAlbum, albumId: 2);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Album.ShouldNotBeNull();
        delta.Album.Old.ShouldBe(oldAlbum);
        delta.Album.New.ShouldBe(newAlbum);
        delta.Album.Old!.ArtistName.ShouldBe("Old Artist");
        delta.Album.New!.ArtistName.ShouldBe("New Artist");
        delta.Album.Old!.ArtistId.ShouldBe(10L);
        delta.Album.New!.ArtistId.ShouldBe(20L);
        delta.AlbumId.ShouldNotBeNull();
        delta.AlbumId.Old.ShouldBe(1L);
        delta.AlbumId.New.ShouldBe(2L);
        delta.Title.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_AlbumArtistChanged_ReturnsAlbumFieldChange()
    {
        // Same album id+title, but the album artist changed — the album FieldChange
        // must still be produced so the viewer can surface the artist-only change.
        var oldAlbum = new SongSnapshotAlbum { Id = 1, Title = "Same Album", ArtistId = 10, ArtistName = "Old Artist" };
        var newAlbum = new SongSnapshotAlbum { Id = 1, Title = "Same Album", ArtistId = 20, ArtistName = "New Artist" };
        var oldSnapshot = BuildSnapshot(album: oldAlbum, albumId: 1);
        var newSnapshot = BuildSnapshot(album: newAlbum, albumId: 1);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Album.ShouldNotBeNull();
        delta.Album.Old.ShouldBe(oldAlbum);
        delta.Album.New.ShouldBe(newAlbum);
        delta.Album.Old!.ArtistName.ShouldBe("Old Artist");
        delta.Album.New!.ArtistName.ShouldBe("New Artist");
        delta.Album.Old!.ArtistId.ShouldBe(10L);
        delta.Album.New!.ArtistId.ShouldBe(20L);
        delta.AlbumId.ShouldBeNull("AlbumId did not change — only the artist did");
        delta.Title.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_CoverChanged_ReturnsCoverFieldChange()
    {
        var oldCover = new SongSnapshotCover { Id = 1, MimeType = "image/jpeg", Width = 100, Height = 100, Data = "old" };
        var newCover = new SongSnapshotCover { Id = 2, MimeType = "image/png", Width = 200, Height = 200, Data = "new" };
        var oldSnapshot = BuildSnapshot(cover: oldCover, coverId: 1);
        var newSnapshot = BuildSnapshot(cover: newCover, coverId: 2);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Cover.ShouldNotBeNull();
        delta.Cover.Old.ShouldBe(oldCover);
        delta.Cover.New.ShouldBe(newCover);
        delta.CoverId.ShouldNotBeNull();
        delta.CoverId.Old.ShouldBe(1L);
        delta.CoverId.New.ShouldBe(2L);
        delta.Title.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_SameCoverId_OldCoverNullFromTrigger_ReturnsNullCoverChange()
    {
        // The trigger only includes the cover object when cover_id changes. When
        // a non-cover field changes, the queue snapshot has cover = null while
        // the live state (fetched with includeCover: true) has the cover present.
        // Same CoverId on both sides must suppress the false cover change.
        var liveCover = new SongSnapshotCover { Id = 5, MimeType = "image/jpeg", Width = 50, Height = 50, Data = "live" };
        var queueSnapshot = BuildSnapshot(cover: null, coverId: 5);
        var liveSnapshot = BuildSnapshot(cover: liveCover, coverId: 5);

        var delta = _service.ComputeDiff(queueSnapshot, liveSnapshot);

        delta.Cover.ShouldBeNull("Same CoverId must suppress the false cover diff");
        delta.CoverId.ShouldBeNull("CoverId did not change");
    }

    [Fact]
    public void ComputeDiff_SameCoverId_NewCoverNullFromTrigger_ReturnsNullCoverChange()
    {
        // Symmetric case: live state has cover, queue snapshot (newer, after a
        // non-cover change) has cover = null. Same CoverId ⇒ no cover change.
        var liveCover = new SongSnapshotCover { Id = 7, MimeType = "image/png", Width = 10, Height = 10, Data = "abc" };
        var liveSnapshot = BuildSnapshot(cover: liveCover, coverId: 7);
        var queueSnapshot = BuildSnapshot(cover: null, coverId: 7);

        var delta = _service.ComputeDiff(liveSnapshot, queueSnapshot);

        delta.Cover.ShouldBeNull("Same CoverId must suppress the false cover diff");
    }

    [Fact]
    public void ComputeDiff_BothCoversNull_SameCoverIdNull_ReturnsNullCoverChange()
    {
        var oldSnapshot = BuildSnapshot(cover: null, coverId: null);
        var newSnapshot = BuildSnapshot(cover: null, coverId: null);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Cover.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_CoverAdded_CoverIdNullToValue_ReturnsCoverChange()
    {
        var newCover = new SongSnapshotCover { Id = 9, MimeType = "image/jpeg", Width = 1, Height = 1, Data = "new" };
        var oldSnapshot = BuildSnapshot(cover: null, coverId: null);
        var newSnapshot = BuildSnapshot(cover: newCover, coverId: 9);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Cover.ShouldNotBeNull();
        delta.Cover.Old.ShouldBeNull();
        delta.Cover.New.ShouldBe(newCover);
        delta.CoverId.ShouldNotBeNull();
        delta.CoverId.Old.ShouldBeNull();
        delta.CoverId.New.ShouldBe(9L);
    }

    [Fact]
    public void ComputeDiff_CoverRemoved_CoverIdToNull_ReturnsCoverChange()
    {
        var oldCover = new SongSnapshotCover { Id = 3, MimeType = "image/jpeg", Width = 1, Height = 1, Data = "old" };
        var oldSnapshot = BuildSnapshot(cover: oldCover, coverId: 3);
        var newSnapshot = BuildSnapshot(cover: null, coverId: null);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Cover.ShouldNotBeNull();
        delta.Cover.Old.ShouldBe(oldCover);
        delta.Cover.New.ShouldBeNull();
        delta.CoverId.ShouldNotBeNull();
        delta.CoverId.Old.ShouldBe(3L);
        delta.CoverId.New.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_NewSnapshotIsNull_ReturnsDeletedActionAndNoFieldChanges()
    {
        var oldSnapshot = BuildSnapshot(title: "Doomed");

        var delta = _service.ComputeDiff(oldSnapshot, null);

        delta.Action.ShouldBe("deleted");
        delta.Title.ShouldBeNull();
        delta.Album.ShouldBeNull();
        delta.Artists.ShouldBeNull();
        delta.Cover.ShouldBeNull();
    }

    [Fact]
    public void ComputeDiff_AllFieldsChanged_ReturnsAllFieldChanges()
    {
        var oldAlbum = new SongSnapshotAlbum { Id = 1, Title = "Old Album" };
        var newAlbum = new SongSnapshotAlbum { Id = 2, Title = "New Album" };
        var oldArtists = new List<SongSnapshotArtist> { new() { Id = 1, Name = "A" } };
        var newArtists = new List<SongSnapshotArtist> { new() { Id = 2, Name = "B" } };
        var oldGenres = new List<SongSnapshotGenre> { new() { Id = 1, Name = "Rock" } };
        var newGenres = new List<SongSnapshotGenre> { new() { Id = 2, Name = "Pop" } };
        var oldSources = new List<SongSnapshotSource> { new() { Id = 1, Name = "S1" } };
        var newSources = new List<SongSnapshotSource> { new() { Id = 2, Name = "S2" } };
        var oldDevices = new List<SongSnapshotDevice> { new() { Id = 1, DevicePath = "/d1", SyncAction = SongSyncAction.Upload } };
        var newDevices = new List<SongSnapshotDevice> { new() { Id = 2, DevicePath = "/d2", SyncAction = SongSyncAction.Remove } };
        var oldCover = new SongSnapshotCover { Id = 1, MimeType = "image/jpeg", Width = 10, Height = 10, Data = "a" };
        var newCover = new SongSnapshotCover { Id = 2, MimeType = "image/png", Width = 20, Height = 20, Data = "b" };

        var oldSnapshot = BuildSnapshot(
            title: "Old", label: "OldLabel", albumId: 1, coverId: 1, year: 2020,
            lyrics: "old", explicitFlag: false, size: 100, track: 1,
            duration: TimeSpan.FromSeconds(60), bitrate: 128, ownerId: 1,
            rating: 3m, isFavorite: false, playCount: 5,
            repositoryPath: "/old.mp3", checksum: "old", checksumAlgorithm: "MD5",
            addedAt: new DateTime(2020, 1, 1), createdAt: new DateTime(2020, 1, 1),
            modifiedAt: new DateTime(2020, 1, 1), fileModifiedAt: new DateTime(2020, 1, 1),
            album: oldAlbum, artists: oldArtists, genres: oldGenres,
            sources: oldSources, devices: oldDevices, cover: oldCover);
        var newSnapshot = BuildSnapshot(
            title: "New", label: "NewLabel", albumId: 2, coverId: 2, year: 2024,
            lyrics: "new", explicitFlag: true, size: 200, track: 2,
            duration: TimeSpan.FromSeconds(120), bitrate: 256, ownerId: 2,
            rating: 5m, isFavorite: true, playCount: 10,
            repositoryPath: "/new.mp3", checksum: "new", checksumAlgorithm: "XxHash128",
            addedAt: new DateTime(2024, 1, 1), createdAt: new DateTime(2024, 1, 1),
            modifiedAt: new DateTime(2024, 1, 1), fileModifiedAt: new DateTime(2024, 1, 1),
            album: newAlbum, artists: newArtists, genres: newGenres,
            sources: newSources, devices: newDevices, cover: newCover);

        var delta = _service.ComputeDiff(oldSnapshot, newSnapshot);

        delta.Title.ShouldNotBeNull();
        delta.Label.ShouldNotBeNull();
        delta.AlbumId.ShouldNotBeNull();
        delta.CoverId.ShouldNotBeNull();
        delta.Year.ShouldNotBeNull();
        delta.Lyrics.ShouldNotBeNull();
        delta.Explicit.ShouldNotBeNull();
        delta.Size.ShouldNotBeNull();
        delta.Track.ShouldNotBeNull();
        delta.Duration.ShouldNotBeNull();
        delta.Bitrate.ShouldNotBeNull();
        delta.OwnerId.ShouldNotBeNull();
        delta.Rating.ShouldNotBeNull();
        delta.IsFavorite.ShouldNotBeNull();
        delta.PlayCount.ShouldNotBeNull();
        delta.RepositoryPath.ShouldNotBeNull();
        delta.Checksum.ShouldNotBeNull();
        delta.ChecksumAlgorithm.ShouldNotBeNull();
        delta.AddedAt.ShouldNotBeNull();
        delta.CreatedAt.ShouldNotBeNull();
        delta.ModifiedAt.ShouldNotBeNull();
        delta.FileModifiedAt.ShouldNotBeNull();
        delta.Album.ShouldNotBeNull();
        delta.Artists.ShouldNotBeNull();
        delta.Genres.ShouldNotBeNull();
        delta.Sources.ShouldNotBeNull();
        delta.Devices.ShouldNotBeNull();
        delta.Cover.ShouldNotBeNull();
    }
}