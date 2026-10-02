using Microsoft.Extensions.DependencyInjection;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

public class MetadataFetchQueueBackgroundJobSpecs : IDisposable
{
    // The substituted scope factory cannot provide a DbContext, so the scheduler started by the queue's constructor
    // fails to pull tasks and never touches the scenario's data
    private readonly MetadataFetchQueue _queue = new(Substitute.For<IServiceScopeFactory>());

    public void Dispose() => _queue.Scheduler.Dispose();

    [Fact]
    public async Task GetCounters_TasksInEveryStatus_CountsQueuedCompletedAndFailed()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        scenario.CreateMetadataFetchTask(song, MetadataFetchStatus.Queued);
        scenario.CreateMetadataFetchTask(song, MetadataFetchStatus.Processing);
        scenario.CreateMetadataFetchTask(song, MetadataFetchStatus.Completed);
        scenario.CreateMetadataFetchTask(song, MetadataFetchStatus.Completed);
        scenario.CreateMetadataFetchTask(song, MetadataFetchStatus.Failed, "boom");

        // Act
        var counters = await _queue.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(1);
        counters.Processed.ShouldBe(2);
        counters.Failed.ShouldBe(1);
    }

    [Fact]
    public async Task GetCounters_TasksOfOtherUsersSongs_AreExcluded()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Other Song", ownerId: other.Id);
        scenario.CreateMetadataFetchTask(song, MetadataFetchStatus.Queued);
        scenario.CreateMetadataFetchTask(song, MetadataFetchStatus.Failed, "boom");

        // Act
        var counters = await _queue.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id, CancellationToken.None);
        var failures = await _queue.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(0);
        counters.Failed.ShouldBe(0);
        failures.Total.ShouldBe(0);
    }

    [Fact]
    public async Task GetFailures_FailedTask_ReturnsErrorAndDebugDetails()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var completedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var task = scenario.CreateMetadataFetchTask(song, MetadataFetchStatus.Failed, "No metadata sources configured",
            MetadataFetchFailureReason.SystemError, completedAt);

        // Act
        var failures = await _queue.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        var failure = failures.Items.ShouldHaveSingleItem();
        failure.Id.ShouldBe(task.Id.ToString());
        failure.Title.ShouldBe("Song");
        failure.Message.ShouldBe("No metadata sources configured");
        failure.OccurredAt.ShouldBe(completedAt);
        failure.Details.ShouldContain(d => d.Label == "SongId" && d.Value == song.Id.ToString());
        failure.Details.ShouldContain(d => d.Label == "FailureReason" && d.Value == "SystemError");
    }
}
