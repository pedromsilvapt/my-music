namespace MyMusic.CLI.Tests.Services.Sync;

using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;
using Shouldly;
using Xunit;

public class AdaptiveChunkSizeTests
{
    private static readonly TimeSpan Target = TimeSpan.FromSeconds(2);

    [Fact]
    public void Current_StartsAtTheConfiguredSize()
    {
        Create(new ChunkSizeRange(50, 10, 1000)).Current.ShouldBe(50);
    }

    [Fact]
    public void Report_FullRequestUnderHalfTheTarget_DoublesTheSize()
    {
        var chunkSize = Create(new ChunkSizeRange(50, 10, 1000));

        chunkSize.Report(50, TimeSpan.FromMilliseconds(999));

        chunkSize.Current.ShouldBe(100);
    }

    [Fact]
    public void Report_RequestBetweenHalfTheTargetAndTheTarget_KeepsTheSize()
    {
        var chunkSize = Create(new ChunkSizeRange(50, 10, 1000));

        chunkSize.Report(50, TimeSpan.FromSeconds(1));
        chunkSize.Report(50, TimeSpan.FromSeconds(2));

        chunkSize.Current.ShouldBe(50);
    }

    [Fact]
    public void Report_FastRequestWithFewerItemsThanTheSize_KeepsTheSize()
    {
        // The last request of a step is rarely full: it says little about a bigger one
        var chunkSize = Create(new ChunkSizeRange(50, 10, 1000));

        chunkSize.Report(49, TimeSpan.FromMilliseconds(1));

        chunkSize.Current.ShouldBe(50);
    }

    [Fact]
    public void Report_RequestOverTheTarget_ShrinksTheSizeInProportion()
    {
        var chunkSize = Create(new ChunkSizeRange(100, 10, 1000));

        chunkSize.Report(100, TimeSpan.FromSeconds(2.5));

        chunkSize.Current.ShouldBe(80);
    }

    [Fact]
    public void Report_RequestFarOverTheTarget_HalvesTheSizeAtMost()
    {
        var chunkSize = Create(new ChunkSizeRange(100, 10, 1000));

        chunkSize.Report(100, TimeSpan.FromSeconds(60));

        chunkSize.Current.ShouldBe(50);
    }

    [Fact]
    public void Report_NeverLeavesTheRange()
    {
        var chunkSize = Create(new ChunkSizeRange(50, 40, 60));

        chunkSize.Report(50, TimeSpan.FromMilliseconds(1));
        chunkSize.Current.ShouldBe(60);

        chunkSize.Report(60, TimeSpan.FromSeconds(60));
        chunkSize.Report(40, TimeSpan.FromSeconds(60));
        chunkSize.Current.ShouldBe(40);
    }

    [Fact]
    public void Report_NotAdaptive_KeepsTheSize()
    {
        var chunkSize = new AdaptiveChunkSize(
            new ChunkSizeRange(50, 10, 1000),
            new SyncChunkTuning { Adaptive = false, TargetRequestDuration = Target });

        chunkSize.Report(50, TimeSpan.FromMilliseconds(1));
        chunkSize.Report(50, TimeSpan.FromSeconds(60));

        chunkSize.Current.ShouldBe(50);
    }

    [Theory]
    [InlineData(5, 10, 1000, 5, 1000, 5)]
    [InlineData(2000, 10, 1000, 2000, 2000, 10)]
    [InlineData(0, 0, 0, 1, 1, 1)]
    public void Constructor_SizeOutsideTheRange_WidensTheRangeToIt(int size, int min, int max, int expectedStart, int expectedMax, int expectedMin)
    {
        var chunkSize = Create(new ChunkSizeRange(size, min, max));
        chunkSize.Current.ShouldBe(expectedStart);

        // Growing reaches the widened maximum, shrinking the widened minimum
        for (var i = 0; i < 20; i++)
        {
            chunkSize.Report(chunkSize.Current, TimeSpan.FromMilliseconds(1));
        }
        chunkSize.Current.ShouldBe(expectedMax);

        for (var i = 0; i < 20; i++)
        {
            chunkSize.Report(chunkSize.Current, TimeSpan.FromSeconds(60));
        }
        chunkSize.Current.ShouldBe(expectedMin);
    }

    private static AdaptiveChunkSize Create(ChunkSizeRange range) =>
        new(range, new SyncChunkTuning { Adaptive = true, TargetRequestDuration = Target });
}
