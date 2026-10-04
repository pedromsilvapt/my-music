using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.SongHistory.Models;
using NSubstitute;
using Shouldly;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Tests.Services.SongHistory;

public class SongHistoryBaselineBackfillServiceSpecs
{
    private readonly Scenario _scenario = new();
    private readonly ISongHistorySnapshotService _snapshots = Substitute.For<ISongHistorySnapshotService>();
    private readonly ISongHistoryNotifier _notifier = Substitute.For<ISongHistoryNotifier>();

    private SongHistoryBaselineBackfillService CreateService() => new(
        _scenario.DbContext,
        _snapshots,
        new SongHistoryDiffService(),
        new SongHistoryThumbnailService(Substitute.For<ILogger<SongHistoryThumbnailService>>()),
        _notifier,
        Substitute.For<ILogger<SongHistoryBaselineBackfillService>>());

    private Song CreateSongWithCurrentTitle(string title)
    {
        var song = _scenario.CreateSong(title);
        _snapshots.GetCurrentSnapshotAsync(song.Id, true, Arg.Any<CancellationToken>()).Returns(new SongSnapshot
        {
            Id = song.Id,
            Title = title,
            Label = title,
            RepositoryPath = song.RepositoryPath,
            Checksum = song.Checksum,
            ChecksumAlgorithm = song.ChecksumAlgorithm,
            Action = "updated",
        });
        return song;
    }

    private void AddTitleRevision(Song song, int revision, string oldTitle, string newTitle)
    {
        _scenario.DbContext.SongHistories.Add(new SongHistoryEntity
        {
            SongId = song.Id,
            OwnerId = song.OwnerId,
            SongRevision = revision,
            Diff = new SongHistoryDelta
            {
                Action = "updated",
                Title = new FieldChange<string> { Old = oldTitle, New = newTitle },
            },
            CreatedAt = DateTime.UtcNow,
        });
        _scenario.DbContext.SaveChanges();
    }

    private void AddQueueEntry(Song song, int errorCount = 0)
    {
        _scenario.DbContext.SongHistoryQueues.Add(new SongHistoryQueue
        {
            SongId = song.Id,
            OwnerId = song.OwnerId,
            SongRevision = 1,
            Data = new SongSnapshot
            {
                Title = song.Title,
                Label = song.Label,
                RepositoryPath = song.RepositoryPath,
                Checksum = song.Checksum,
                ChecksumAlgorithm = song.ChecksumAlgorithm,
                Action = "updated",
            },
            CreatedAt = DateTime.UtcNow,
            ErrorCount = errorCount,
        });
        _scenario.DbContext.SaveChanges();
    }

    private List<SongHistoryEntity> HistoryOf(Song song) => _scenario.DbContext.SongHistories
        .Where(h => h.SongId == song.Id)
        .OrderBy(h => h.SongRevision)
        .ToList();

    [Fact]
    public async Task BackfillBatch_SongWithoutHistory_RecordsCurrentStateAsBaseline()
    {
        var song = CreateSongWithCurrentTitle("Song");

        var (candidates, recorded) = await CreateService().BackfillBatchAsync(50, CancellationToken.None);

        candidates.ShouldBe(1);
        recorded.ShouldBe(1);
        var baseline = HistoryOf(song).ShouldHaveSingleItem();
        baseline.SongRevision.ShouldBe(1);
        baseline.OwnerId.ShouldBe(song.OwnerId);
        baseline.Action.ShouldBe("created");
        baseline.Diff.Action.ShouldBe("created");
        baseline.Diff.Title.ShouldNotBeNull();
        baseline.Diff.Title.Old.ShouldBeNull();
        baseline.Diff.Title.New.ShouldBe("Song");
        _notifier.Received(1).Publish(song.Id, SongHistoryNotificationKind.Processed);
    }

    [Fact]
    public async Task BackfillBatch_SongWithHistory_RecordsEarliestStateBeforeExistingRevisions()
    {
        var song = CreateSongWithCurrentTitle("C");
        AddTitleRevision(song, 1, "A", "B");
        AddTitleRevision(song, 2, "B", "C");

        await CreateService().BackfillBatchAsync(50, CancellationToken.None);

        var history = HistoryOf(song);
        history.Select(h => h.SongRevision).ShouldBe([1, 2, 3]);
        history[0].Action.ShouldBe("created");
        history[0].Diff.Title.ShouldNotBeNull().New.ShouldBe("A");
        history[1].Diff.Title.ShouldNotBeNull().Old.ShouldBe("A");
        history[1].Diff.Title.New.ShouldBe("B");
        history[2].Diff.Title.ShouldNotBeNull().Old.ShouldBe("B");
        history[2].Diff.Title.New.ShouldBe("C");
    }

    [Fact]
    public async Task BackfillBatch_SongWithPendingChanges_IsSkippedUntilTheyAreProcessed()
    {
        var song = CreateSongWithCurrentTitle("Song");
        AddQueueEntry(song);

        var (candidates, recorded) = await CreateService().BackfillBatchAsync(50, CancellationToken.None);

        candidates.ShouldBe(1);
        recorded.ShouldBe(0);
        HistoryOf(song).ShouldBeEmpty();
    }

    [Fact]
    public async Task BackfillBatch_SongWithDeadLetteredChanges_IsNotACandidate()
    {
        var song = CreateSongWithCurrentTitle("Song");
        AddQueueEntry(song, errorCount: SongHistoryWorker.MaxErrorCount);

        var (candidates, _) = await CreateService().BackfillBatchAsync(50, CancellationToken.None);

        candidates.ShouldBe(0);
    }

    [Fact]
    public async Task BackfillBatch_SecondRun_FindsNothingLeft()
    {
        CreateSongWithCurrentTitle("Song");
        await CreateService().BackfillBatchAsync(50, CancellationToken.None);

        var (candidates, recorded) = await CreateService().BackfillBatchAsync(50, CancellationToken.None);

        candidates.ShouldBe(0);
        recorded.ShouldBe(0);
    }
}
