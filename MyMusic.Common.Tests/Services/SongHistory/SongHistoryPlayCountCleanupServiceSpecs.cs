using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.SongHistory.Models;
using NSubstitute;
using Shouldly;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Tests.Services.SongHistory;

public class SongHistoryPlayCountCleanupServiceSpecs
{
    private readonly Scenario _scenario = new();
    private readonly ISongHistoryNotifier _notifier = Substitute.For<ISongHistoryNotifier>();

    private SongHistoryPlayCountCleanupService CreateService() => new(
        _scenario.DbContext,
        _notifier,
        Substitute.For<ILogger<SongHistoryPlayCountCleanupService>>());

    private void AddRevision(long songId, int revision, SongHistoryDelta diff)
    {
        _scenario.DbContext.SongHistories.Add(new SongHistoryEntity
        {
            SongId = songId,
            SongRevision = revision,
            Diff = diff,
            Action = diff.Action ?? SongHistoryEntity.UpdatedAction,
            CreatedAt = DateTime.UtcNow,
        });
        _scenario.DbContext.SaveChanges();
    }

    private static SongHistoryDelta Created(string title) => new()
    {
        Action = SongHistoryEntity.CreatedAction,
        Title = new FieldChange<string> { New = title },
    };

    private static SongHistoryDelta TitleChange(string oldTitle, string newTitle) => new()
    {
        Action = SongHistoryEntity.UpdatedAction,
        Title = new FieldChange<string> { Old = oldTitle, New = newTitle },
    };

    private static SongHistoryDelta PlayCountChange(int oldCount, int newCount, string? action = "updated") => new()
    {
        Action = action,
        PlayCount = new FieldChange<int> { Old = oldCount, New = newCount },
    };

    private List<SongHistoryEntity> HistoryOf(long songId)
    {
        _scenario.DbContext.ChangeTracker.Clear();
        return _scenario.DbContext.SongHistories
            .Where(h => h.SongId == songId)
            .OrderBy(h => h.SongRevision)
            .ToList();
    }

    [Fact]
    public async Task CleanupBatch_PlayCountOnlyRevisions_AreRemovedAndRevisionsCompacted()
    {
        var song = _scenario.CreateSong("Song");
        AddRevision(song.Id, 1, Created("A"));
        AddRevision(song.Id, 2, PlayCountChange(0, 1));
        AddRevision(song.Id, 3, TitleChange("A", "B"));
        AddRevision(song.Id, 4, PlayCountChange(1, 2));
        AddRevision(song.Id, 5, PlayCountChange(2, 3, action: null));
        AddRevision(song.Id, 6, TitleChange("B", "C"));

        var (songs, removed, _) = await CreateService().CleanupBatchAsync(0, 50, CancellationToken.None);

        songs.ShouldBe(1);
        removed.ShouldBe(3);
        var history = HistoryOf(song.Id);
        history.Select(h => h.SongRevision).ShouldBe([1, 2, 3]);
        history[0].Action.ShouldBe(SongHistoryEntity.CreatedAction);
        history[1].Diff.Title.ShouldNotBeNull().New.ShouldBe("B");
        history[2].Diff.Title.ShouldNotBeNull().New.ShouldBe("C");
        _notifier.Received(1).Publish(song.Id, SongHistoryNotificationKind.Processed);
    }

    [Fact]
    public async Task CleanupBatch_PlayCountMixedWithOtherChanges_IsKept()
    {
        var song = _scenario.CreateSong("Song");
        AddRevision(song.Id, 1, Created("A"));
        AddRevision(song.Id, 2, TitleChange("A", "B") with
        {
            PlayCount = new FieldChange<int> { Old = 0, New = 1 },
        });

        var (_, removed, _) = await CreateService().CleanupBatchAsync(0, 50, CancellationToken.None);

        removed.ShouldBe(0);
        HistoryOf(song.Id).Select(h => h.SongRevision).ShouldBe([1, 2]);
        _notifier.DidNotReceive().Publish(Arg.Any<long>(), Arg.Any<SongHistoryNotificationKind>());
    }

    [Fact]
    public async Task CleanupBatch_DeletedRevisionCarryingPlayCount_IsKept()
    {
        var song = _scenario.CreateSong("Song");
        AddRevision(song.Id, 1, Created("A"));
        AddRevision(song.Id, 2, PlayCountChange(0, 1, action: "deleted"));

        var (_, removed, _) = await CreateService().CleanupBatchAsync(0, 50, CancellationToken.None);

        removed.ShouldBe(0);
        HistoryOf(song.Id).Select(h => h.SongRevision).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task CleanupBatch_SongWithoutPlayCountRevisions_IsNotACandidate()
    {
        var song = _scenario.CreateSong("Song");
        AddRevision(song.Id, 1, Created("A"));
        AddRevision(song.Id, 2, TitleChange("A", "B"));

        var (songs, removed, nextSongId) = await CreateService().CleanupBatchAsync(0, 50, CancellationToken.None);

        songs.ShouldBe(0);
        removed.ShouldBe(0);
        nextSongId.ShouldBeNull();
        HistoryOf(song.Id).Select(h => h.SongRevision).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task CleanupBatch_DeletedSong_HistoryIsCleanedUpToo()
    {
        const long deletedSongId = 999_999;
        AddRevision(deletedSongId, 1, Created("A"));
        AddRevision(deletedSongId, 2, PlayCountChange(0, 1));
        AddRevision(deletedSongId, 3, new SongHistoryDelta { Action = "deleted" });

        var (_, removed, _) = await CreateService().CleanupBatchAsync(0, 50, CancellationToken.None);

        removed.ShouldBe(1);
        var history = HistoryOf(deletedSongId);
        history.Select(h => h.SongRevision).ShouldBe([1, 2]);
        history[1].Action.ShouldBe("deleted");
    }

    [Fact]
    public async Task CleanupBatch_PagesThroughSongsByIdUntilNothingIsLeft()
    {
        var first = _scenario.CreateSong("First");
        var second = _scenario.CreateSong("Second");
        foreach (var song in new[] { first, second })
        {
            AddRevision(song.Id, 1, Created("A"));
            AddRevision(song.Id, 2, PlayCountChange(0, 1));
        }

        var service = CreateService();

        var (songs, removed, nextSongId) = await service.CleanupBatchAsync(0, 1, CancellationToken.None);
        songs.ShouldBe(1);
        removed.ShouldBe(1);
        nextSongId.ShouldBe(first.Id);
        HistoryOf(second.Id).Count.ShouldBe(2);

        (songs, removed, nextSongId) = await service.CleanupBatchAsync(nextSongId.Value, 1, CancellationToken.None);
        songs.ShouldBe(1);
        removed.ShouldBe(1);
        nextSongId.ShouldBe(second.Id);
        HistoryOf(second.Id).ShouldHaveSingleItem();

        (songs, _, nextSongId) = await service.CleanupBatchAsync(nextSongId.Value, 1, CancellationToken.None);
        songs.ShouldBe(0);
        nextSongId.ShouldBeNull();
    }
}
