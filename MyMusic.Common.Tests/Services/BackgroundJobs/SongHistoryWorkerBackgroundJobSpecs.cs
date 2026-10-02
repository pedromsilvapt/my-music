using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Services.SongHistory;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

public class SongHistoryWorkerBackgroundJobSpecs
{
    private readonly SongHistoryWorker _worker = new(
        Substitute.For<IServiceScopeFactory>(),
        Options.Create(new Config { MusicRepositoryPath = "/data" }),
        Substitute.For<ISongHistoryNotifier>(),
        Substitute.For<ILogger<SongHistoryWorker>>());

    [Fact]
    public async Task GetCounters_PendingAndDeadLetteredEntries_CountsQueuedAndFailedOnly()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        scenario.CreateSongHistoryQueueEntry(song.Id, 1);
        scenario.CreateSongHistoryQueueEntry(song.Id, 2, errorCount: 1, lastError: "retrying");
        scenario.CreateSongHistoryQueueEntry(song.Id, 3, errorCount: SongHistoryWorker.MaxErrorCount, lastError: "dead");
        scenario.CreateSongHistoryQueueEntry(song.Id, 4, processedAt: DateTime.UtcNow);

        // Act
        var counters = await _worker.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(2);
        counters.Processed.ShouldBeNull();
        counters.Failed.ShouldBe(1);
    }

    [Fact]
    public async Task GetCounters_EntriesOfOtherUsersOrDeletedSongs_AreExcluded()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var otherSong = scenario.CreateSong("Other Song", ownerId: other.Id);
        scenario.CreateSongHistoryQueueEntry(otherSong.Id, 1);
        scenario.CreateSongHistoryQueueEntry(otherSong.Id, 2, errorCount: SongHistoryWorker.MaxErrorCount);
        scenario.CreateSongHistoryQueueEntry(songId: 999_999, revision: 1);

        // Act
        var counters = await _worker.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(0);
        counters.Failed.ShouldBe(0);
    }

    [Fact]
    public async Task GetFailures_DeadLetteredEntry_ReturnsLastErrorAndDebugDetails()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        scenario.CreateSongHistoryQueueEntry(song.Id, 1, errorCount: 1, lastError: "still retrying");
        var entry = scenario.CreateSongHistoryQueueEntry(song.Id, 2, errorCount: SongHistoryWorker.MaxErrorCount,
            lastError: "Snapshot could not be diffed");

        // Act
        var failures = await _worker.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        failures.Total.ShouldBe(1);
        var failure = failures.Items.ShouldHaveSingleItem();
        failure.Id.ShouldBe(entry.Id.ToString());
        failure.Title.ShouldBe("Song (revision 2)");
        failure.Message.ShouldBe("Snapshot could not be diffed");
        failure.Details.ShouldContain(d => d.Label == "SongId" && d.Value == song.Id.ToString());
        failure.Details.ShouldContain(d => d.Label == "ErrorCount" && d.Value == "3");
        failure.Details.ShouldContain(d => d.Label == "TransactionId" && d.Value == "1002");
    }
}
