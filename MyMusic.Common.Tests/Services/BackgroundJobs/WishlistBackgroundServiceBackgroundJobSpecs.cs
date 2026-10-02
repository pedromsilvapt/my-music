using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

public class WishlistBackgroundServiceBackgroundJobSpecs
{
    private readonly WishlistBackgroundService _service = new(
        Substitute.For<IServiceScopeFactory>(),
        Options.Create(new Config { MusicRepositoryPath = "/data" }),
        Substitute.For<ILogger<WishlistBackgroundService>>());

    [Fact]
    public async Task GetCounters_ActiveUpdatedAndFailingItems_CountsQueuedAndFailedOnly()
    {
        // Arrange
        var scenario = new Scenario();
        var source = scenario.CreateSource();
        scenario.CreateWishlistItem(source, scenario.AdminUser, "active");
        scenario.CreateWishlistItem(source, scenario.AdminUser, "failing", continuousFailedCount: 2,
            lastErrorMessage: "boom");
        scenario.CreateWishlistItem(source, scenario.AdminUser, "updated", WishlistItemStatus.Updated);

        // Act
        var counters = await _service.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id,
            CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(2);
        counters.Processed.ShouldBeNull();
        counters.Failed.ShouldBe(1);
    }

    [Fact]
    public async Task GetCounters_OtherUsersItems_AreExcluded()
    {
        // Arrange
        var scenario = new Scenario();
        var source = scenario.CreateSource();
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateWishlistItem(source, other, "failing", continuousFailedCount: 1, lastErrorMessage: "boom");

        // Act
        var counters = await _service.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id,
            CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(0);
        counters.Failed.ShouldBe(0);
    }

    [Fact]
    public async Task GetFailures_FailingItem_ReturnsLastErrorAndDebugDetails()
    {
        // Arrange
        var scenario = new Scenario();
        var source = scenario.CreateSource("Shop");
        var updatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var item = scenario.CreateWishlistItem(source, scenario.AdminUser, "my query", continuousFailedCount: 4,
            lastErrorMessage: "Source unreachable", updatedAt: updatedAt);

        // Act
        var failures = await _service.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        var failure = failures.Items.ShouldHaveSingleItem();
        failure.Id.ShouldBe(item.Id.ToString());
        failure.Title.ShouldBe("my query");
        failure.Message.ShouldBe("Source unreachable");
        failure.OccurredAt.ShouldBe(updatedAt);
        failure.Details.ShouldContain(d => d.Label == "Source" && d.Value == "Shop");
        failure.Details.ShouldContain(d => d.Label == "ContinuousFailedCount" && d.Value == "4");
    }
}
