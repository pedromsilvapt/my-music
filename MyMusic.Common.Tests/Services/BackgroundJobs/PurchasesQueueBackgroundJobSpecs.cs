using Microsoft.Extensions.DependencyInjection;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

public class PurchasesQueueBackgroundJobSpecs : IDisposable
{
    // The substituted scope factory cannot provide a DbContext, so the scheduler started by the queue's constructor
    // fails to pull tasks and never touches the scenario's data
    private readonly PurchasesQueue _queue = new(Substitute.For<IServiceScopeFactory>());

    public void Dispose() => _queue.Scheduler.Dispose();

    [Fact]
    public async Task GetCounters_PurchasesInEveryStatus_CountsQueuedCompletedAndFailed()
    {
        // Arrange
        var scenario = new Scenario();
        var source = scenario.CreateSource();
        var userId = scenario.AdminUser.Id;
        scenario.CreatePurchase(source, userId, PurchasedSongStatus.Queued, "Queued 1");
        scenario.CreatePurchase(source, userId, PurchasedSongStatus.Queued, "Queued 2");
        scenario.CreatePurchase(source, userId, PurchasedSongStatus.Acquiring, "Acquiring");
        scenario.CreatePurchase(source, userId, PurchasedSongStatus.Completed, "Completed");
        scenario.CreatePurchase(source, userId, PurchasedSongStatus.Failed, "Failed", "boom");

        // Act
        var counters = await _queue.GetCountersAsync(scenario.DbContext, userId, CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(2);
        counters.Processed.ShouldBe(1);
        counters.Failed.ShouldBe(1);
    }

    [Fact]
    public async Task GetCounters_NoPurchases_ReportsZeroes()
    {
        // Arrange
        var scenario = new Scenario();

        // Act
        var counters = await _queue.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(0);
        counters.Processed.ShouldBe(0);
        counters.Failed.ShouldBe(0);
    }

    [Fact]
    public async Task GetCounters_OtherUsersPurchases_AreExcluded()
    {
        // Arrange
        var scenario = new Scenario();
        var source = scenario.CreateSource();
        var other = scenario.CreateUser("Other", "other");
        scenario.CreatePurchase(source, other.Id, PurchasedSongStatus.Queued);
        scenario.CreatePurchase(source, other.Id, PurchasedSongStatus.Failed, errorMessage: "boom");

        // Act
        var counters = await _queue.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id, CancellationToken.None);
        var failures = await _queue.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(0);
        counters.Failed.ShouldBe(0);
        failures.Total.ShouldBe(0);
        failures.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFailures_FailedPurchase_ReturnsErrorAndDebugDetails()
    {
        // Arrange
        var scenario = new Scenario();
        var source = scenario.CreateSource("Shop");
        var purchase = scenario.CreatePurchase(source, scenario.AdminUser.Id, PurchasedSongStatus.Failed, "Song",
            "Download timed out");

        // Act
        var failures = await _queue.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        failures.Total.ShouldBe(1);
        var failure = failures.Items.ShouldHaveSingleItem();
        failure.Id.ShouldBe(purchase.Id.ToString());
        failure.Title.ShouldBe("Song (Artist • Album)");
        failure.Message.ShouldBe("Download timed out");
        failure.OccurredAt.ShouldBe(purchase.CreatedAt);
        failure.Details.ShouldContain(d => d.Label == "Source" && d.Value == "Shop");
        failure.Details.ShouldContain(d => d.Label == "ExternalId" && d.Value == "ext-Song");
    }

    [Fact]
    public async Task GetFailures_ManyFailures_PagesMostRecentFirst()
    {
        // Arrange
        var scenario = new Scenario();
        var source = scenario.CreateSource();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            scenario.CreatePurchase(source, scenario.AdminUser.Id, PurchasedSongStatus.Failed, $"Song {i}", "boom",
                start.AddMinutes(i));
        }

        // Act
        var firstPage = await _queue.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 2,
            CancellationToken.None);
        var lastPage = await _queue.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 3, 2,
            CancellationToken.None);

        // Assert
        firstPage.Total.ShouldBe(5);
        firstPage.Items.Select(f => f.Title).ShouldBe(["Song 4 (Artist • Album)", "Song 3 (Artist • Album)"]);
        lastPage.Items.Select(f => f.Title).ShouldBe(["Song 0 (Artist • Album)"]);
    }
}
