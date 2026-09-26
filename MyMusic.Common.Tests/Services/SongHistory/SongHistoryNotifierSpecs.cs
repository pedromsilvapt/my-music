using MyMusic.Common.Services.SongHistory;
using Shouldly;

namespace MyMusic.Common.Tests.Services.SongHistory;

public class SongHistoryNotifierSpecs
{
    [Fact]
    public void Publish_DeliversToSubscribersOfThatSongOnly()
    {
        var notifier = new SongHistoryNotifier();
        using var first = notifier.Subscribe(1);
        using var second = notifier.Subscribe(1);
        using var other = notifier.Subscribe(2);

        notifier.Publish(1, SongHistoryNotificationKind.Processed);

        first.Reader.TryRead(out var firstKind).ShouldBeTrue();
        firstKind.ShouldBe(SongHistoryNotificationKind.Processed);
        second.Reader.TryRead(out var secondKind).ShouldBeTrue();
        secondKind.ShouldBe(SongHistoryNotificationKind.Processed);
        other.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public void Publish_AfterDispose_IsNotDelivered()
    {
        var notifier = new SongHistoryNotifier();
        var subscription = notifier.Subscribe(1);
        using var remaining = notifier.Subscribe(1);

        subscription.Dispose();
        notifier.Publish(1, SongHistoryNotificationKind.Failed);

        subscription.Reader.TryRead(out _).ShouldBeFalse();
        remaining.Reader.TryRead(out var kind).ShouldBeTrue();
        kind.ShouldBe(SongHistoryNotificationKind.Failed);
    }

    [Fact]
    public void Publish_WithoutSubscribers_DoesNotThrow()
    {
        var notifier = new SongHistoryNotifier();
        using (notifier.Subscribe(1))
        {
        }

        Should.NotThrow(() => notifier.Publish(1, SongHistoryNotificationKind.Processed));
    }

    [Fact]
    public void Publish_WhenSubscriberIsFull_DropsOldestWithoutBlocking()
    {
        var notifier = new SongHistoryNotifier();
        using var subscription = notifier.Subscribe(1);

        for (var i = 0; i < 20; i++)
        {
            notifier.Publish(1, SongHistoryNotificationKind.Processed);
        }
        notifier.Publish(1, SongHistoryNotificationKind.Failed);

        var received = new List<SongHistoryNotificationKind>();
        while (subscription.Reader.TryRead(out var kind))
        {
            received.Add(kind);
        }

        received.Count.ShouldBeLessThan(21);
        received[^1].ShouldBe(SongHistoryNotificationKind.Failed);
    }
}
