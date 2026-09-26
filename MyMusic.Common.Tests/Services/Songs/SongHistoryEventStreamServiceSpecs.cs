using Microsoft.Extensions.Options;
using MyMusic.Common.Services;
using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.Songs;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongHistoryEventStreamServiceSpecs
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Scenario _scenario = new();
    private readonly SongHistoryNotifier _notifier = new();

    private SongHistoryEventStreamService CreateService(long? userId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(userId ?? _scenario.AdminUser.Id);
        return new SongHistoryEventStreamService(
            _scenario.DbContext,
            currentUser,
            new SongHistoryPendingService(_scenario.DbContext),
            _notifier,
            Options.Create(new Config { MusicRepositoryPath = "/data", SongHistoryWorkerIntervalSeconds = 60 }));
    }

    [Fact]
    public async Task StreamAsync_NothingPending_CompletesImmediately()
    {
        var song = _scenario.CreateSong("Song");

        var events = await CreateService().StreamAsync(song.Id).ToListAsync().AsTask().WaitAsync(Timeout);

        events.ShouldBe([new SongHistoryEvent(SongHistoryEventType.Complete, song.Id)]);
    }

    [Fact]
    public async Task StreamAsync_SongNotOwned_CompletesImmediately()
    {
        var otherUser = _scenario.CreateUser("Other", "other");
        var song = _scenario.CreateSong("Song", ownerId: otherUser.Id);
        SongHistoryPendingServiceSpecs.AddQueueEntry(_scenario, song.Id);

        var events = await CreateService().StreamAsync(song.Id).ToListAsync().AsTask().WaitAsync(Timeout);

        events.ShouldBe([new SongHistoryEvent(SongHistoryEventType.Complete, song.Id)]);
    }

    [Fact]
    public async Task StreamAsync_ProcessedWhilePending_EmitsProcessedThenCompletesWhenQueueDrains()
    {
        var song = _scenario.CreateSong("Song");
        var first = SongHistoryPendingServiceSpecs.AddQueueEntry(_scenario, song.Id);
        await using var stream = CreateService().StreamAsync(song.Id).GetAsyncEnumerator();

        // Start waiting, then process one batch while another entry remains queued
        var next = stream.MoveNextAsync().AsTask();
        var second = SongHistoryPendingServiceSpecs.AddQueueEntry(_scenario, song.Id);
        _scenario.DbContext.SongHistoryQueues.Remove(first);
        await _scenario.DbContext.SaveChangesAsync();
        await PublishUntilCompleted(song.Id, SongHistoryNotificationKind.Processed, next);

        (await next).ShouldBeTrue();
        stream.Current.ShouldBe(new SongHistoryEvent(SongHistoryEventType.Processed, song.Id));

        // Drain the queue and notify: should emit processed, then complete and end
        next = stream.MoveNextAsync().AsTask();
        _scenario.DbContext.SongHistoryQueues.Remove(second);
        await _scenario.DbContext.SaveChangesAsync();
        await PublishUntilCompleted(song.Id, SongHistoryNotificationKind.Processed, next);

        (await next).ShouldBeTrue();
        stream.Current.ShouldBe(new SongHistoryEvent(SongHistoryEventType.Processed, song.Id));
        (await stream.MoveNextAsync().AsTask().WaitAsync(Timeout)).ShouldBeTrue();
        stream.Current.ShouldBe(new SongHistoryEvent(SongHistoryEventType.Complete, song.Id));
        (await stream.MoveNextAsync().AsTask().WaitAsync(Timeout)).ShouldBeFalse();
    }

    [Fact]
    public async Task StreamAsync_FailedAndDeadLettered_CompletesWithoutProcessedEvent()
    {
        var song = _scenario.CreateSong("Song");
        var entry = SongHistoryPendingServiceSpecs.AddQueueEntry(_scenario, song.Id);
        await using var stream = CreateService().StreamAsync(song.Id).GetAsyncEnumerator();

        var next = stream.MoveNextAsync().AsTask();
        entry.ErrorCount = SongHistoryWorker.MaxErrorCount;
        await _scenario.DbContext.SaveChangesAsync();
        await PublishUntilCompleted(song.Id, SongHistoryNotificationKind.Failed, next);

        (await next).ShouldBeTrue();
        stream.Current.ShouldBe(new SongHistoryEvent(SongHistoryEventType.Complete, song.Id));
        (await stream.MoveNextAsync().AsTask().WaitAsync(Timeout)).ShouldBeFalse();
    }

    /// <summary>
    /// The stream subscribes asynchronously once enumeration starts, so keep publishing until the
    /// pending <paramref name="next"/> call observes it.
    /// </summary>
    private async Task PublishUntilCompleted(long songId, SongHistoryNotificationKind kind, Task next)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!next.IsCompleted && DateTime.UtcNow < deadline)
        {
            _notifier.Publish(songId, kind);
            await Task.WhenAny(next, Task.Delay(50));
        }
    }
}
