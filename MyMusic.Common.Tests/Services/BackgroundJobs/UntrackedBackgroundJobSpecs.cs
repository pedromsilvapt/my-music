using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Services;
using MyMusic.Common.Services.BackgroundJobs;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

/// <summary>
/// Jobs that keep no record, or only a partial one, of the work they do.
/// </summary>
public class UntrackedBackgroundJobSpecs
{
    [Fact]
    public async Task BitrateBackfill_GetCounters_CountsOwnedSongsWithoutBitrateAsQueued()
    {
        // Arrange
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateSong("Missing 1", bitrate: null);
        scenario.CreateSong("Missing 2", bitrate: null);
        scenario.CreateSong("Known", bitrate: 320);
        scenario.CreateSong("Other Missing", ownerId: other.Id, bitrate: null);
        var service = new BitrateBackfillService(
            Substitute.For<IServiceScopeFactory>(),
            Options.Create(new Config { MusicRepositoryPath = "/data" }),
            new MockFileSystem(),
            Substitute.For<ILogger<BitrateBackfillService>>());

        // Act
        var counters = await service.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id,
            CancellationToken.None);
        var failures = await service.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        counters.ShouldBe(new BackgroundJobCounters(2, null, null));
        failures.Total.ShouldBe(0);
    }

    [Fact]
    public async Task MetadataFetchCleanup_ReportsNothingTracked()
    {
        // Arrange
        var scenario = new Scenario();
        var service = new MetadataFetchCleanupService(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<ILogger<MetadataFetchCleanupService>>());

        // Act
        var counters = await service.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id,
            CancellationToken.None);
        var failures = await service.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        counters.ShouldBe(BackgroundJobCounters.NotTracked);
        failures.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task StagingDirectoryCleanup_ReportsNothingTracked()
    {
        // Arrange
        var scenario = new Scenario();
        var service = new StagingDirectoryCleanupService(
            Substitute.For<IServiceScopeFactory>(),
            new MockFileSystem(),
            Substitute.For<ILogger<StagingDirectoryCleanupService>>());

        // Act
        var counters = await service.GetCountersAsync(scenario.DbContext, scenario.AdminUser.Id,
            CancellationToken.None);
        var failures = await service.GetFailuresAsync(scenario.DbContext, scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        counters.ShouldBe(BackgroundJobCounters.NotTracked);
        failures.Items.ShouldBeEmpty();
    }
}
