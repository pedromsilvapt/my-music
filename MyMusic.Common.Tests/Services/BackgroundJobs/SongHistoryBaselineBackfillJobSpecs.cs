using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.SongHistory.Models;
using Shouldly;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

public class SongHistoryBaselineBackfillJobSpecs
{
    private readonly Scenario _scenario = new();
    private readonly SongHistoryBaselineBackfillJob _job = new();

    [Fact]
    public async Task GetCounters_SongsWithAndWithoutBaseline_CountsOnlySongsMissingIt()
    {
        // Arrange
        var withBaseline = _scenario.CreateSong("With Baseline");
        _scenario.CreateSongHistoryRevision(withBaseline.Id, 1, new SongHistoryDelta(),
            SongHistoryEntity.CreatedAction);
        var withoutBaseline = _scenario.CreateSong("Without Baseline");
        _scenario.CreateSongHistoryRevision(withoutBaseline.Id, 1, new SongHistoryDelta
        {
            Title = new FieldChange<string> { Old = "Old", New = "Without Baseline" },
        });
        _scenario.CreateSong("No History");

        // Act
        var counters = await _job.GetCountersAsync(_scenario.DbContext, _scenario.AdminUser.Id,
            CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(2);
        counters.Processed.ShouldBeNull();
        counters.Failed.ShouldBeNull();
    }

    [Fact]
    public async Task GetCounters_SongsWithQueueEntriesOrOfOtherUsers_AreExcluded()
    {
        // Arrange
        var pending = _scenario.CreateSong("Pending");
        _scenario.CreateSongHistoryQueueEntry(pending.Id, 1);
        var deadLettered = _scenario.CreateSong("Dead Lettered");
        _scenario.CreateSongHistoryQueueEntry(deadLettered.Id, 1, errorCount: SongHistoryWorker.MaxErrorCount);
        var processed = _scenario.CreateSong("Processed");
        _scenario.CreateSongHistoryQueueEntry(processed.Id, 1, processedAt: DateTime.UtcNow);
        var other = _scenario.CreateUser("Other", "other");
        _scenario.CreateSong("Other Song", ownerId: other.Id);

        // Act
        var counters = await _job.GetCountersAsync(_scenario.DbContext, _scenario.AdminUser.Id,
            CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(1);
    }

    [Fact]
    public async Task GetFailures_ReturnsEmptyPage()
    {
        // Act
        var failures = await _job.GetFailuresAsync(_scenario.DbContext, _scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        failures.Total.ShouldBe(0);
        failures.Items.ShouldBeEmpty();
    }
}
