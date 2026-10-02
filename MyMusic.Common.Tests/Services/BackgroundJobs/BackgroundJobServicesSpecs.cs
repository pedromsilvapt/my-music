using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyMusic.Common.Services.BackgroundJobs;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

public class BackgroundJobServicesSpecs
{
    [Fact]
    public async Task List_RegisteredJobs_ReturnsEachJobsCountersInRegistrationOrder()
    {
        // Arrange
        var scenario = new Scenario();
        var first = CreateJob("first", new BackgroundJobCounters(1, 2, 3));
        var second = CreateJob("second", BackgroundJobCounters.NotTracked);
        var service = new BackgroundJobListService(scenario.DbContext, [first, second]);

        // Act
        var summaries = await service.ListAsync(scenario.AdminUser.Id, CancellationToken.None);

        // Assert
        summaries.ShouldBe([
            new BackgroundJobSummary("first", new BackgroundJobCounters(1, 2, 3)),
            new BackgroundJobSummary("second", BackgroundJobCounters.NotTracked),
        ]);
        await first.Received(1).GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListFailures_KnownKey_DelegatesToThatJob()
    {
        // Arrange
        var scenario = new Scenario();
        var page = new BackgroundJobFailurePage(1, [new BackgroundJobFailure("1", "Title", "boom", null, [])]);
        var other = CreateJob("other", BackgroundJobCounters.NotTracked);
        var target = CreateJob("target", BackgroundJobCounters.NotTracked);
        target.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 2, 10, Arg.Any<CancellationToken>())
            .Returns(page);
        var service = new BackgroundJobFailureListService(scenario.DbContext, [other, target]);

        // Act
        var result = await service.ListAsync("target", scenario.AdminUser.Id, 2, 10, CancellationToken.None);

        // Assert
        result.ShouldBeSameAs(page);
        await other.DidNotReceiveWithAnyArgs().GetFailuresAsync(default!, default, default, default, default);
    }

    [Fact]
    public async Task ListFailures_UnknownKey_ThrowsKeyNotFound()
    {
        // Arrange
        var scenario = new Scenario();
        var service = new BackgroundJobFailureListService(scenario.DbContext,
            [CreateJob("known", BackgroundJobCounters.NotTracked)]);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(() =>
            service.ListAsync("unknown", scenario.AdminUser.Id, 1, 20, CancellationToken.None));
    }

    [Fact]
    public void AddHostedBackgroundJob_HostedServiceAndJob_AreTheSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHostedBackgroundJob<FakeHostedJob>();

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        var hosted = provider.GetServices<IHostedService>().ShouldHaveSingleItem();
        var job = provider.GetServices<IQueuedBackgroundJob>().ShouldHaveSingleItem();
        hosted.ShouldBeSameAs(job);
        job.ShouldBeSameAs(provider.GetRequiredService<FakeHostedJob>());
    }

    [Fact]
    public void AddBackgroundJob_Job_IsNotHosted()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBackgroundJob<FakeHostedJob>();

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetServices<IHostedService>().ShouldBeEmpty();
        provider.GetServices<IQueuedBackgroundJob>().ShouldHaveSingleItem()
            .ShouldBeSameAs(provider.GetRequiredService<FakeHostedJob>());
    }

    private static IQueuedBackgroundJob CreateJob(string key, BackgroundJobCounters counters)
    {
        var job = Substitute.For<IQueuedBackgroundJob>();
        job.Key.Returns(key);
        job.GetCountersAsync(Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(counters);
        return job;
    }

    public class FakeHostedJob : BackgroundService, IQueuedBackgroundJob
    {
        public string Key => "fake";

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

        public Task<BackgroundJobCounters> GetCountersAsync(MusicDbContext db, long userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(BackgroundJobCounters.NotTracked);

        public Task<BackgroundJobFailurePage> GetFailuresAsync(MusicDbContext db, long userId, int page, int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(BackgroundJobFailurePage.Empty);
    }
}
