using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.SongHistory.Models;
using MyMusic.Common.Services.Songs;
using NSubstitute;
using Shouldly;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongHistoryVersionDiffServiceSpecs
{
    private static (SongHistoryVersionDiffService service, ICurrentUser currentUser) CreateService(
        Scenario scenario,
        long? userId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(userId ?? scenario.AdminUser.Id);
        var service = new SongHistoryVersionDiffService(scenario.DbContext, currentUser);
        return (service, currentUser);
    }

    /// <summary>
    /// Builds a delta that carries only the fields explicitly provided. Unspecified
    /// fields are absent (null FieldChange), which is exactly how the worker writes
    /// deltas for single-field edits. This mirrors the real trigger→worker pipeline.
    /// </summary>
    private static SongHistoryDelta BuildDelta(
        string? action = null,
        FieldChange<string>? title = null,
        FieldChange<string>? label = null,
        FieldChange<long>? albumId = null,
        FieldChange<long?>? coverId = null,
        FieldChange<int?>? year = null,
        FieldChange<string?>? lyrics = null,
        FieldChange<bool>? explicitFlag = null,
        FieldChange<long>? size = null,
        FieldChange<int?>? track = null,
        FieldChange<TimeSpan>? duration = null,
        FieldChange<int?>? bitrate = null,
        FieldChange<long>? ownerId = null,
        FieldChange<decimal?>? rating = null,
        FieldChange<bool>? isFavorite = null,
        FieldChange<int>? playCount = null,
        FieldChange<string>? repositoryPath = null,
        FieldChange<string>? checksum = null,
        FieldChange<string>? checksumAlgorithm = null,
        FieldChange<DateTime?>? addedAt = null,
        FieldChange<DateTime>? createdAt = null,
        FieldChange<DateTime>? modifiedAt = null,
        FieldChange<DateTime?>? fileModifiedAt = null,
        FieldChange<SongSnapshotAlbum?>? album = null,
        FieldChange<List<SongSnapshotArtist>>? artists = null,
        FieldChange<List<SongSnapshotGenre>>? genres = null,
        FieldChange<List<SongSnapshotSource>>? sources = null,
        FieldChange<List<SongSnapshotDevice>>? devices = null,
        FieldChange<SongSnapshotCover?>? cover = null) => new()
        {
            Action = action,
            Title = title,
            Label = label,
            AlbumId = albumId,
            CoverId = coverId,
            Year = year,
            Lyrics = lyrics,
            Explicit = explicitFlag,
            Size = size,
            Track = track,
            Duration = duration,
            Bitrate = bitrate,
            OwnerId = ownerId,
            Rating = rating,
            IsFavorite = isFavorite,
            PlayCount = playCount,
            RepositoryPath = repositoryPath,
            Checksum = checksum,
            ChecksumAlgorithm = checksumAlgorithm,
            AddedAt = addedAt,
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt,
            FileModifiedAt = fileModifiedAt,
            Album = album,
            Artists = artists,
            Genres = genres,
            Sources = sources,
            Devices = devices,
            Cover = cover,
        };

    /// <summary>
    /// A full "created" delta as the worker writes for the very first revision:
    /// every scalar/object field carries the seed values. Used to set up a
    /// realistic first-revision baseline.
    /// </summary>
    private static SongHistoryDelta BuildCreatedDelta(
        string title = "Song",
        string label = "Song - Album",
        long albumId = 1,
        long? coverId = null,
        int? year = 2024,
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
        string? albumTitle = null,
        string[]? artistNames = null,
        string[]? genreNames = null,
        string[]? sourceNames = null,
        string? coverData = null,
        string? coverMime = null)
    {
        var now = DateTime.UtcNow;
        var album = albumTitle == null ? null : new SongSnapshotAlbum { Id = albumId, Title = albumTitle };
        var artists = (artistNames ?? ["Artist A"])
            .Select((n, i) => new SongSnapshotArtist { Id = i + 1, Name = n }).ToList();
        var genres = (genreNames ?? [])
            .Select((n, i) => new SongSnapshotGenre { Id = i + 1, Name = n }).ToList();
        var sources = (sourceNames ?? [])
            .Select((n, i) => new SongSnapshotSource { Id = i + 1, Name = n }).ToList();
        var cover = coverData == null ? null : new SongSnapshotCover
        {
            Id = coverId ?? 1,
            MimeType = coverMime ?? "image/jpeg",
            Width = 100,
            Height = 100,
            Data = coverData,
        };

        return BuildDelta(
            action: "created",
            title: new() { Old = null, New = title },
            label: new() { Old = null, New = label },
            albumId: new() { Old = 0, New = albumId },
            coverId: coverId == null ? null : new() { Old = null, New = coverId },
            year: new() { Old = null, New = year },
            lyrics: new() { Old = null, New = lyrics },
            explicitFlag: new() { Old = false, New = explicitFlag },
            size: new() { Old = 0, New = size },
            track: new() { Old = null, New = track },
            duration: duration == null ? null : new() { Old = TimeSpan.Zero, New = duration.Value },
            bitrate: new() { Old = null, New = bitrate },
            ownerId: new() { Old = 0, New = ownerId },
            rating: new() { Old = null, New = rating },
            isFavorite: new() { Old = false, New = isFavorite },
            playCount: new() { Old = 0, New = playCount },
            repositoryPath: new() { Old = "", New = repositoryPath },
            checksum: new() { Old = "", New = checksum },
            checksumAlgorithm: new() { Old = "", New = checksumAlgorithm },
            addedAt: new() { Old = null, New = addedAt },
            createdAt: new() { Old = DateTime.UnixEpoch, New = createdAt ?? now },
            modifiedAt: new() { Old = DateTime.UnixEpoch, New = modifiedAt ?? now },
            fileModifiedAt: new() { Old = null, New = fileModifiedAt },
            album: new() { Old = null, New = album },
            artists: new() { Old = [], New = artists },
            genres: new() { Old = [], New = genres },
            sources: new() { Old = [], New = sources },
            cover: new() { Old = null, New = cover });
    }

    private static SongHistoryEntity CreateHistoryRow(
        long songId,
        int revision,
        SongHistoryDelta diff,
        DateTime? createdAt = null) =>
        new()
        {
            SongId = songId,
            SongRevision = revision,
            Diff = diff,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };

    [Fact]
    public async Task GetVersionDiffAsync_SongNotOwned_ReturnsNull()
    {
        // Arrange - a song owned by another user with history
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherSong = scenario.CreateSong("Other's Song", ownerId: otherUser.Id);
        var snapshot = BuildCreatedDelta(title: "Other");
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(otherSong.Id, 1, snapshot));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario, userId: scenario.AdminUser.Id);

        // Act
        var result = await service.GetVersionDiffAsync(otherSong.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_HistoryNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("My Song");
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 99999, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_HistoryIdBelongsToDifferentSong_ReturnsNull()
    {
        // Arrange - two songs owned by the current user, each with history
        var scenario = new Scenario();
        var song1 = scenario.CreateSong("Song One");
        var song2 = scenario.CreateSong("Song Two");
        var snap1 = BuildCreatedDelta(title: "Song One");
        var snap2 = BuildCreatedDelta(title: "Song Two");
        var h1 = CreateHistoryRow(song1.Id, 1, snap1);
        var h2 = CreateHistoryRow(song2.Id, 1, snap2);
        scenario.DbContext.SongHistories.AddRange(h1, h2);
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act - request song1's route but h2's id (cross-song)
        var result = await service.GetVersionDiffAsync(song1.Id, h2.Id, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_FirstRevision_OldRevisionAndDateNull()
    {
        // Arrange - a single revision (no predecessor) carrying a title + explicit change
        var scenario = new Scenario();
        var song = scenario.CreateSong("First");
        var createdAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var delta = BuildDelta(
            action: "created",
            title: new() { Old = null, New = "First Title" },
            explicitFlag: new() { Old = false, New = true });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta, createdAt));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - no predecessor revision metadata
        result.ShouldNotBeNull();
        result.OldRevision.ShouldBeNull();
        result.OldVersionDate.ShouldBeNull();
        result.NewRevision.ShouldBe(1);
        result.NewVersionDate.ShouldBe(createdAt);
    }

    [Fact]
    public async Task GetVersionDiffAsync_SecondRevision_OldRevisionFromPredecessor()
    {
        // Arrange - two revisions; the second only changed the title
        var scenario = new Scenario();
        var song = scenario.CreateSong("Changed");
        var oldDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var newDate = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var firstDelta = BuildCreatedDelta(title: "Old Title");
        var secondDelta = BuildDelta(
            action: "updated",
            title: new() { Old = "Old Title", New = "New Title" });
        scenario.DbContext.SongHistories.AddRange(
            CreateHistoryRow(song.Id, 1, firstDelta, oldDate),
            CreateHistoryRow(song.Id, 2, secondDelta, newDate));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act - diff the second revision against the first
        var historyId = scenario.DbContext.SongHistories
            .First(h => h.SongId == song.Id && h.SongRevision == 2).Id;
        var result = await service.GetVersionDiffAsync(song.Id, historyId, CancellationToken.None);

        // Assert - predecessor metadata comes from revision 1
        result.ShouldNotBeNull();
        result.OldRevision.ShouldBe(1);
        result.OldVersionDate.ShouldBe(oldDate);
        result.NewRevision.ShouldBe(2);
        result.NewVersionDate.ShouldBe(newDate);
    }

    [Fact]
    public async Task GetVersionDiffAsync_ThirdRevision_DiffsAgainstImmediatePredecessorOnly()
    {
        // Arrange - three revisions; diffing revision 3 should report revision 2 as old
        var scenario = new Scenario();
        var song = scenario.CreateSong("Three Revisions");
        var snap1 = BuildCreatedDelta(title: "V1");
        var snap2 = BuildDelta(action: "updated", title: new() { Old = "V1", New = "V2" });
        var snap3 = BuildDelta(action: "updated", title: new() { Old = "V2", New = "V3" });
        scenario.DbContext.SongHistories.AddRange(
            CreateHistoryRow(song.Id, 1, snap1),
            CreateHistoryRow(song.Id, 2, snap2),
            CreateHistoryRow(song.Id, 3, snap3));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act - diff revision 3 (should compare against revision 2, not 1)
        var historyId = scenario.DbContext.SongHistories
            .First(h => h.SongId == song.Id && h.SongRevision == 3).Id;
        var result = await service.GetVersionDiffAsync(song.Id, historyId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.OldRevision.ShouldBe(2);
        result.NewRevision.ShouldBe(3);
        result.Metadata.Title.ShouldNotBeNull();
        result.Metadata.Title.Old.ShouldBe("V2");
        result.Metadata.Title.New.ShouldBe("V3");
    }

    [Fact]
    public async Task GetVersionDiffAsync_OnlyExplicitChanged_OnlyExplicitFieldPresent()
    {
        // Arrange - a single revision whose delta carries only the explicit field.
        // This is the exact scenario reported in the bug: changing isExplicit on a
        // song with a title/artists/genres/cover must NOT surface those unchanged
        // fields in the diff.
        var scenario = new Scenario();
        var song = scenario.CreateSong("Explicit Only");
        var delta = BuildDelta(
            action: "updated",
            explicitFlag: new() { Old = false, New = true });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - ONLY Explicit is populated; every other field is null (absent).
        result.ShouldNotBeNull();
        var md = result.Metadata;
        md.Explicit.ShouldNotBeNull();
        md.Explicit.Old.ShouldBe(false);
        md.Explicit.New.ShouldBe(true);

        md.Title.ShouldBeNull("Title did not change — must be absent");
        md.Artists.ShouldBeNull("Artists did not change — must be absent");
        md.Genres.ShouldBeNull("Genres did not change — must be absent");
        md.Album.ShouldBeNull("Album did not change — must be absent");
        md.Cover.ShouldBeNull("Cover did not change — must be absent (asymmetry fix)");
        md.Year.ShouldBeNull();
        md.Lyrics.ShouldBeNull();
        md.Rating.ShouldBeNull();
        md.Label.ShouldBeNull();
        md.AlbumId.ShouldBeNull();
        md.CoverId.ShouldBeNull();
        md.Size.ShouldBeNull();
        md.Track.ShouldBeNull();
        md.Duration.ShouldBeNull();
        md.Bitrate.ShouldBeNull();
        md.OwnerId.ShouldBeNull();
        md.IsFavorite.ShouldBeNull();
        md.PlayCount.ShouldBeNull();
        md.RepositoryPath.ShouldBeNull();
        md.Checksum.ShouldBeNull();
        md.ChecksumAlgorithm.ShouldBeNull();
        md.AddedAt.ShouldBeNull();
        md.CreatedAt.ShouldBeNull();
        md.ModifiedAt.ShouldBeNull();
        md.FileModifiedAt.ShouldBeNull();
        md.Sources.ShouldBeNull();
        md.Devices.ShouldBeNull();
        md.AlbumArtist.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_TitleChanged_OnlyTitleFieldPresent()
    {
        // Arrange - a delta carrying only a title change
        var scenario = new Scenario();
        var song = scenario.CreateSong("Title Change");
        var delta = BuildDelta(
            action: "updated",
            title: new() { Old = "Old Title", New = "New Title" });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - only Title is present
        result.ShouldNotBeNull();
        var md = result.Metadata;
        md.Title.ShouldNotBeNull();
        md.Title.Old.ShouldBe("Old Title");
        md.Title.New.ShouldBe("New Title");
        md.Explicit.ShouldBeNull();
        md.Artists.ShouldBeNull();
        md.Genres.ShouldBeNull();
        md.Cover.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_CoverChanged_CoverFieldPresentWithDataUrl()
    {
        // Arrange - a delta carrying a cover change (CoverId null→5)
        var scenario = new Scenario();
        var song = scenario.CreateSong("Cover Change");
        var newCover = new SongSnapshotCover
        {
            Id = 5,
            MimeType = "image/png",
            Width = 100,
            Height = 100,
            Data = "TkVX",
        };
        var delta = BuildDelta(
            action: "updated",
            coverId: new() { Old = null, New = 5 },
            cover: new() { Old = null, New = newCover });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Cover.ShouldNotBeNull();
        result.Metadata.Cover.Old.ShouldBeNull();
        result.Metadata.Cover.New.ShouldBe("data:image/png;base64,TkVX");
        result.Metadata.CoverId.ShouldNotBeNull();
        result.Metadata.CoverId.Old.ShouldBeNull();
        result.Metadata.CoverId.New.ShouldBe(5);
        result.Metadata.Title.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_CoverUnchangedAsymmetry_CoverFieldAbsent()
    {
        // Arrange - the asymmetry scenario: a non-cover edit produces a delta that
        // carries NO cover FieldChange (the worker's DiffCover now suppresses it
        // because CoverId is unchanged). The viewer must therefore omit Cover.
        var scenario = new Scenario();
        var song = scenario.CreateSong("Asymmetry");
        var delta = BuildDelta(
            action: "updated",
            explicitFlag: new() { Old = false, New = true });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - Cover is absent because the delta carries no cover change
        result.ShouldNotBeNull();
        result.Metadata.Cover.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_CoverRemoved_CoverFieldPresentWithNullNew()
    {
        // Arrange - cover was removed: CoverId 5→null, cover object→null
        var scenario = new Scenario();
        var song = scenario.CreateSong("Cover Removed");
        var oldCover = new SongSnapshotCover
        {
            Id = 5,
            MimeType = "image/jpeg",
            Width = 50,
            Height = 50,
            Data = "T0xE",
        };
        var delta = BuildDelta(
            action: "updated",
            coverId: new() { Old = 5, New = null },
            cover: new() { Old = oldCover, New = null });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Cover.ShouldNotBeNull();
        result.Metadata.Cover.Old.ShouldBe("data:image/jpeg;base64,T0xE");
        result.Metadata.Cover.New.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_ArtistsChanged_ArtistsFieldPresent()
    {
        // Arrange - artists list changed
        var scenario = new Scenario();
        var song = scenario.CreateSong("Artist Change");
        var oldArtists = new List<SongSnapshotArtist>
        {
            new() { Id = 1, Name = "Artist A" },
            new() { Id = 2, Name = "Artist B" },
        };
        var newArtists = new List<SongSnapshotArtist>
        {
            new() { Id = 3, Name = "Artist C" },
        };
        var delta = BuildDelta(
            action: "updated",
            artists: new() { Old = oldArtists, New = newArtists });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Artists.ShouldNotBeNull();
        result.Metadata.Artists.Old!.Select(a => a.Name).ShouldBe(["Artist A", "Artist B"]);
        result.Metadata.Artists.New!.Select(a => a.Name).ShouldBe(["Artist C"]);
        result.Metadata.Title.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_GenresChanged_GenresFieldPresent()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Genre Change");
        var oldGenres = new List<SongSnapshotGenre>
        {
            new() { Id = 1, Name = "Rock" },
            new() { Id = 2, Name = "Pop" },
        };
        var newGenres = new List<SongSnapshotGenre>
        {
            new() { Id = 3, Name = "Jazz" },
        };
        var delta = BuildDelta(
            action: "updated",
            genres: new() { Old = oldGenres, New = newGenres });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Genres.ShouldNotBeNull();
        result.Metadata.Genres.Old.ShouldBe(["Rock", "Pop"]);
        result.Metadata.Genres.New.ShouldBe(["Jazz"]);
    }

    [Fact]
    public async Task GetVersionDiffAsync_DevicesChanged_DevicesFieldPresent()
    {
        // Arrange - devices sync state changed
        var scenario = new Scenario();
        var song = scenario.CreateSong("Device Change");
        var oldDevices = new List<SongSnapshotDevice>
        {
            new() { Id = 1, DevicePath = "/mnt/phone", SyncAction = SongSyncAction.Upload },
        };
        var newDevices = new List<SongSnapshotDevice>
        {
            new() { Id = 1, DevicePath = "/mnt/phone", SyncAction = SongSyncAction.Remove },
        };
        var delta = BuildDelta(
            action: "updated",
            devices: new() { Old = oldDevices, New = newDevices });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - Devices carries the old/new lists with sync action names
        result.ShouldNotBeNull();
        result.Metadata.Devices.ShouldNotBeNull();
        result.Metadata.Devices.Old!.ShouldHaveSingleItem();
        result.Metadata.Devices.Old[0].DevicePath.ShouldBe("/mnt/phone");
        result.Metadata.Devices.Old[0].SyncAction.ShouldBe("Upload");
        result.Metadata.Devices.New!.ShouldHaveSingleItem();
        result.Metadata.Devices.New[0].SyncAction.ShouldBe("Remove");
    }

    [Fact]
    public async Task GetVersionDiffAsync_ChecksumChanged_ChecksumFieldPresent()
    {
        // Arrange - file replaced, checksum changed
        var scenario = new Scenario();
        var song = scenario.CreateSong("Checksum Change");
        var delta = BuildDelta(
            action: "updated",
            checksum: new() { Old = "oldhash", New = "newhash" },
            checksumAlgorithm: new() { Old = "MD5", New = "XxHash128" });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Checksum.ShouldNotBeNull();
        result.Metadata.Checksum.Old.ShouldBe("oldhash");
        result.Metadata.Checksum.New.ShouldBe("newhash");
        result.Metadata.ChecksumAlgorithm.ShouldNotBeNull();
        result.Metadata.ChecksumAlgorithm.Old.ShouldBe("MD5");
        result.Metadata.ChecksumAlgorithm.New.ShouldBe("XxHash128");
    }

    [Fact]
    public async Task GetVersionDiffAsync_AlbumChanged_AlbumFieldPresent()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Album Change");
        var oldAlbum = new SongSnapshotAlbum { Id = 1, Title = "Old Album" };
        var newAlbum = new SongSnapshotAlbum { Id = 2, Title = "New Album" };
        var delta = BuildDelta(
            action: "updated",
            albumId: new() { Old = 1, New = 2 },
            album: new() { Old = oldAlbum, New = newAlbum });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Album.ShouldNotBeNull();
        result.Metadata.Album.Old!.Name.ShouldBe("Old Album");
        result.Metadata.Album.New!.Name.ShouldBe("New Album");
        result.Metadata.AlbumId.ShouldNotBeNull();
        result.Metadata.AlbumId.Old.ShouldBe(1L);
        result.Metadata.AlbumId.New.ShouldBe(2L);
    }

    [Fact]
    public async Task GetVersionDiffAsync_SourcesChanged_SourcesFieldPresent()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Source Change");
        var oldSources = new List<SongSnapshotSource>
        {
            new() { Id = 1, Name = "Spotify" },
        };
        var newSources = new List<SongSnapshotSource>
        {
            new() { Id = 1, Name = "Spotify" },
            new() { Id = 2, Name = "LastFM" },
        };
        var delta = BuildDelta(
            action: "updated",
            sources: new() { Old = oldSources, New = newSources });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Sources.ShouldNotBeNull();
        result.Metadata.Sources.Old!.Select(s => s.Name).ShouldBe(["Spotify"]);
        result.Metadata.Sources.New!.Select(s => s.Name).ShouldBe(["Spotify", "LastFM"]);
    }

    [Fact]
    public async Task GetVersionDiffAsync_CoverWithNullData_CoverNewIsNull()
    {
        // Arrange - a cover change where the new cover has no data (thumbnail failed)
        var scenario = new Scenario();
        var song = scenario.CreateSong("Null Cover Data");
        var newCover = new SongSnapshotCover
        {
            Id = 1,
            MimeType = "image/jpeg",
            Width = 100,
            Height = 100,
            Data = "",
        };
        var delta = BuildDelta(
            action: "updated",
            cover: new() { Old = null, New = newCover });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - empty data yields a null cover URL
        result.ShouldNotBeNull();
        result.Metadata.Cover.ShouldNotBeNull();
        result.Metadata.Cover.New.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_CoverWithoutMime_DefaultsToImageJpeg()
    {
        // Arrange - a cover with data but an empty mime type
        var scenario = new Scenario();
        var song = scenario.CreateSong("No Mime");
        var newCover = new SongSnapshotCover
        {
            Id = 1,
            MimeType = "",
            Width = 10,
            Height = 10,
            Data = "QUJD",
        };
        var delta = BuildDelta(
            action: "updated",
            cover: new() { Old = null, New = newCover });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Cover.New.ShouldBe("data:image/jpeg;base64,QUJD");
    }

    [Fact]
    public async Task GetVersionDiffAsync_FirstRevisionWithFullDelta_OnlyCarriedFieldsPresent()
    {
        // Arrange - first revision carries a full created delta; only those fields
        // the delta carries should be present (all of them, in this case).
        var scenario = new Scenario();
        var song = scenario.CreateSong("Full First");
        var createdAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var delta = BuildCreatedDelta(
            title: "First Title",
            year: 2020,
            lyrics: "la la la",
            rating: 4.5m,
            explicitFlag: true,
            albumTitle: "First Album",
            artistNames: ["Artist A", "Artist B"],
            genreNames: ["Rock", "Pop"],
            coverData: "QUJD",
            coverMime: "image/png",
            coverId: 9,
            createdAt: createdAt,
            modifiedAt: createdAt);
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta, createdAt));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - every carried field is present with Old=null (first revision)
        result.ShouldNotBeNull();
        result.OldRevision.ShouldBeNull();
        result.OldVersionDate.ShouldBeNull();
        var md = result.Metadata;
        md.Title.ShouldNotBeNull();
        md.Title.Old.ShouldBeNull();
        md.Title.New.ShouldBe("First Title");
        md.Year.New.ShouldBe(2020);
        md.Lyrics.New.ShouldBe("la la la");
        md.Rating.New.ShouldBe(4.5m);
        md.Explicit.New.ShouldBe(true);
        md.Album.New!.Name.ShouldBe("First Album");
        md.Artists.New!.Select(a => a.Name).ShouldBe(["Artist A", "Artist B"]);
        md.Genres.New.ShouldBe(["Rock", "Pop"]);
        md.Cover.New.ShouldBe("data:image/png;base64,QUJD");
    }

    [Fact]
    public async Task GetVersionDiffAsync_AlbumArtistChanged_AlbumArtistFieldPresent()
    {
        // Arrange - the album itself stays (same id+title) but the album artist
        // changed. The delta carries an album FieldChange with differing
        // ArtistId/ArtistName; the viewer must surface a dedicated AlbumArtist
        // field plus the album with its new ArtistName.
        var scenario = new Scenario();
        var song = scenario.CreateSong("Album Artist Change");
        var oldAlbum = new SongSnapshotAlbum { Id = 1, Title = "Same Album", ArtistId = 10, ArtistName = "Old Artist" };
        var newAlbum = new SongSnapshotAlbum { Id = 1, Title = "Same Album", ArtistId = 20, ArtistName = "New Artist" };
        var delta = BuildDelta(
            action: "updated",
            album: new() { Old = oldAlbum, New = newAlbum });
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - AlbumArtist is populated from the album delta's ArtistName
        result.ShouldNotBeNull();
        result.Metadata.AlbumArtist.ShouldNotBeNull();
        result.Metadata.AlbumArtist.Old.ShouldBe("Old Artist");
        result.Metadata.AlbumArtist.New.ShouldBe("New Artist");
        result.Metadata.Album.ShouldNotBeNull();
        result.Metadata.Album.New!.ArtistName.ShouldBe("New Artist");
        result.Metadata.Album.Old!.ArtistName.ShouldBe("Old Artist");
        result.Metadata.AlbumId.ShouldBeNull("AlbumId did not change — only the artist did");
    }

    [Fact]
    public async Task GetVersionDiffAsync_OldShapeDeltaWithoutAlbumArtist_DeserializesWithNulls()
    {
        // Arrange - a delta JSON in the OLD shape: the album object omits
        // artist_id/artist_name entirely. This proves existing history rows,
        // written before album-artist tracking was added, still deserialize and
        // load without throwing, with the artist fields defaulting to null.
        var scenario = new Scenario();
        var song = scenario.CreateSong("Old Shape");
        var json = """
                   {
                     "action": "updated",
                     "album_id": { "old": 1, "new": 2 },
                     "album": {
                       "old": { "id": 1, "title": "Old Album" },
                       "new": { "id": 2, "title": "New Album" }
                     }
                   }
                   """;
        var delta = JsonSerializer.Deserialize<SongHistoryDelta>(json, SongHistoryJsonOptions.Options)!;
        delta.Album!.Old!.ArtistId.ShouldBeNull();
        delta.Album!.New!.ArtistId.ShouldBeNull();
        delta.Album!.Old!.ArtistName.ShouldBeNull();
        delta.Album!.New!.ArtistName.ShouldBeNull();
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, delta));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(song.Id, 1, CancellationToken.None);

        // Assert - deserialization succeeded; artist fields are null but present
        result.ShouldNotBeNull();
        result.Metadata.Album.ShouldNotBeNull();
        result.Metadata.Album.Old!.Name.ShouldBe("Old Album");
        result.Metadata.Album.New!.Name.ShouldBe("New Album");
        result.Metadata.Album.Old!.ArtistName.ShouldBeNull();
        result.Metadata.Album.New!.ArtistName.ShouldBeNull();
        result.Metadata.AlbumArtist.ShouldBeNull();
    }

    [Fact]
    public async Task GetVersionDiffAsync_SongDeleted_ReturnsNull()
    {
        // Arrange - seed a song with history, then delete the song row (history persists).
        var scenario = new Scenario();
        var song = scenario.CreateSong("Doomed");
        var snapshot = BuildCreatedDelta(title: "Doomed");
        scenario.DbContext.SongHistories.Add(CreateHistoryRow(song.Id, 1, snapshot));
        await scenario.DbContext.SaveChangesAsync();
        var songId = song.Id;
        var historyId = scenario.DbContext.SongHistories.First(h => h.SongId == songId).Id;

        scenario.DbContext.Songs.Remove(song);
        await scenario.DbContext.SaveChangesAsync();

        // History row must still be present in the table.
        scenario.DbContext.SongHistories.Count(h => h.SongId == songId).ShouldBe(1);

        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetVersionDiffAsync(songId, historyId, CancellationToken.None);

        // Assert - ownership can no longer be verified, so null.
        result.ShouldBeNull();
    }
}