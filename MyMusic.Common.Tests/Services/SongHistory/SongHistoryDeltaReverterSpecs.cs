using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.SongHistory.Models;
using Shouldly;

namespace MyMusic.Common.Tests.Services.SongHistory;

public class SongHistoryDeltaReverterSpecs
{
    private static readonly SongSnapshotCover CurrentCover = new()
    {
        Id = 2,
        MimeType = "image/jpeg",
        Width = 10,
        Height = 10,
        Data = "current",
    };

    private static readonly SongSnapshot Current = new()
    {
        Title = "New Title",
        Label = "Label",
        Year = 2024,
        RepositoryPath = "/music/song.mp3",
        Checksum = "abc",
        ChecksumAlgorithm = "XxHash128",
        Action = "updated",
        CoverId = 2,
        Cover = CurrentCover,
        Album = new SongSnapshotAlbum { Id = 2, Title = "New Album" },
        Artists = [new SongSnapshotArtist { Id = 2, Name = "New Artist" }],
    };

    [Fact]
    public void Revert_RestoresChangedFieldsOnly()
    {
        var delta = new SongHistoryDelta
        {
            Action = "updated",
            Title = new FieldChange<string> { Old = "Old Title", New = "New Title" },
            Year = new FieldChange<int?> { Old = null, New = 2024 },
            Album = new FieldChange<SongSnapshotAlbum?>
            {
                Old = new SongSnapshotAlbum { Id = 1, Title = "Old Album" },
                New = Current.Album,
            },
            Artists = new FieldChange<List<SongSnapshotArtist>>
            {
                Old = [new SongSnapshotArtist { Id = 1, Name = "Old Artist" }],
                New = Current.Artists,
            },
        };

        var reverted = SongHistoryDeltaReverter.Revert(Current, delta);

        reverted.Title.ShouldBe("Old Title");
        reverted.Year.ShouldBeNull();
        reverted.Album.ShouldNotBeNull().Title.ShouldBe("Old Album");
        reverted.Artists.Select(a => a.Name).ShouldBe(["Old Artist"]);
        // Fields the revision did not change keep their value
        reverted.Label.ShouldBe("Label");
        reverted.CoverId.ShouldBe(2);
        reverted.Cover.ShouldBe(CurrentCover);
    }

    [Fact]
    public void Revert_MergedSongsChange_RestoresPreviousMergedSongs()
    {
        var before = new SongSnapshotMergedSong { Id = 10, Kind = SongMergeKind.ImportDuplicate };
        var merged = new SongSnapshotMergedSong { Id = 11, Kind = SongMergeKind.SoundalikeMerge };
        var current = Current with { MergedSongs = [before, merged] };
        var delta = new SongHistoryDelta
        {
            MergedSongs = new FieldChange<List<SongSnapshotMergedSong>> { Old = [before], New = [before, merged] },
        };

        var reverted = SongHistoryDeltaReverter.Revert(current, delta);

        reverted.MergedSongs.ShouldBe([before]);
        // A revision that did not merge anything leaves the merged songs as they are
        SongHistoryDeltaReverter.Revert(current, new SongHistoryDelta()).MergedSongs.ShouldBe([before, merged]);
    }

    [Fact]
    public void Revert_CoverChange_RestoresPreviousCover()
    {
        var oldCover = CurrentCover with { Id = 1, Data = "old" };
        var delta = new SongHistoryDelta
        {
            CoverId = new FieldChange<long?> { Old = 1, New = 2 },
            Cover = new FieldChange<SongSnapshotCover?> { Old = oldCover, New = CurrentCover },
        };

        var reverted = SongHistoryDeltaReverter.Revert(Current, delta);

        reverted.CoverId.ShouldBe(1);
        reverted.Cover.ShouldBe(oldCover);
    }

    [Fact]
    public void Revert_CoverIdChangeWithoutCover_DropsTheMismatchedCover()
    {
        var delta = new SongHistoryDelta { CoverId = new FieldChange<long?> { Old = 1, New = 2 } };

        var reverted = SongHistoryDeltaReverter.Revert(Current, delta);

        reverted.CoverId.ShouldBe(1);
        reverted.Cover.ShouldBeNull();
    }
}
