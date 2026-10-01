using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncStartServiceSpecs
{
    private static SyncStartService CreateService(Scenario scenario, ISyncActionsServerFactory? factory = null, bool fpcalcAvailable = true)
    {
        var fpcalc = Substitute.For<IFpcalcService>();
        fpcalc.IsAvailable().Returns(fpcalcAvailable);
        return new(
            scenario.DbContext,
            new DeviceLookupService(),
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            fpcalc,
            Substitute.For<ILogger<SyncStartService>>());
    }

    [Fact]
    public async Task StartAsync_Deduplicate_StoresItOnTheSession()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id, new SyncStartInput { Deduplicate = true }, CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessions.Single(s => s.Id == result!.SessionId).Deduplicate.ShouldBeTrue();
    }

    [Fact]
    public async Task StartAsync_DeduplicateWithoutFpcalc_FailsWithoutCreatingSession()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario, fpcalcAvailable: false);

        // Act & Assert
        await Should.ThrowAsync<SyncStartValidationException>(() =>
            service.StartAsync(device.Id, scenario.AdminUser.Id, new SyncStartInput { Deduplicate = true }, CancellationToken.None));
        scenario.DbContext.DeviceSyncSessions.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task StartAsync_WithoutDeduplicate_DoesNotRequireFpcalc()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario, fpcalcAvailable: false);

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id, new SyncStartInput(), CancellationToken.None);

        // Assert
        scenario.DbContext.DeviceSyncSessions.Single(s => s.Id == result!.SessionId).Deduplicate.ShouldBeFalse();
    }

    [Fact]
    public async Task StartAsync_DeviceNotFound_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService(scenario);

        // Act
        var result = await service.StartAsync(9999, scenario.AdminUser.Id, new SyncStartInput(), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task StartAsync_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);
        var service = CreateService(scenario);

        // Act
        var result = await service.StartAsync(otherDevice.Id, scenario.AdminUser.Id, new SyncStartInput(), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
        scenario.DbContext.DeviceSyncSessions.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task StartAsync_CreatesInProgressSessionWithRequestFields()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var repoPath = "/music";
        var beforeStart = DateTime.UtcNow;
        var service = CreateService(scenario);

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id,
            new SyncStartInput { DryRun = true, RepositoryPath = repoPath }, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.SessionId.ShouldBeGreaterThan(0);
        var session = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == result.SessionId);
        session.DeviceId.ShouldBe(device.Id);
        session.Status.ShouldBe(SyncSessionStatus.InProgress);
        session.IsDryRun.ShouldBeTrue();
        session.RepositoryPath.ShouldBe(repoPath);
        session.StartedAt.ShouldBeGreaterThanOrEqualTo(beforeStart);
    }

    [Theory]
    [InlineData(SyncDirection.Both)]
    [InlineData(SyncDirection.Up)]
    [InlineData(SyncDirection.Down)]
    public async Task StartAsync_StoresDirectionOnSession(SyncDirection direction)
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id,
            new SyncStartInput { Direction = direction }, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var session = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == result.SessionId);
        session.Direction.ShouldBe(direction);
    }

    [Fact]
    public async Task StartAsync_NoDirection_DefaultsToBoth()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id, new SyncStartInput(), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var session = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == result.SessionId);
        session.Direction.ShouldBe(SyncDirection.Both);
    }

    [Fact]
    public async Task StartAsync_DefaultInput_CreatesNonDryRunSessionWithNullRepositoryPath()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var service = CreateService(scenario);

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id, new SyncStartInput(), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var session = await scenario.DbContext.DeviceSyncSessions.FirstAsync(s => s.Id == result.SessionId);
        session.IsDryRun.ShouldBeFalse();
        session.RepositoryPath.ShouldBeNull();
    }

    [Fact]
    public async Task StartAsync_WithScanErrors_RecordsErrorActionsForEachScanError()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var factory = Substitute.For<ISyncActionsServerFactory>();
        var syncActions = Substitute.For<ISyncActionsServer>();
        factory.Create(Arg.Any<MusicDbContext>(), Arg.Any<long>(), device.Id, Arg.Any<bool>()).Returns(syncActions);
        var service = CreateService(scenario, factory);

        var input = new SyncStartInput
        {
            ScanErrors =
            [
                new SyncStartScanError { FilePath = "/a.mp3", ErrorMessage = "read failed" },
                new SyncStartScanError { FilePath = "/b.mp3", ErrorMessage = "checksum" },
            ],
        };

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id, input, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        factory.Received(1).Create(Arg.Any<MusicDbContext>(), result.SessionId, device.Id, Arg.Any<bool>());
        await syncActions.Received(1).ActionError("/a.mp3", "read failed", reason: "Scan error: read failed", cancellationToken: CancellationToken.None);
        await syncActions.Received(1).ActionError("/b.mp3", "checksum", reason: "Scan error: checksum", cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WithEmptyScanErrorsList_DoesNotInvokeSyncActions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var factory = Substitute.For<ISyncActionsServerFactory>();
        var service = CreateService(scenario, factory);

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id,
            new SyncStartInput { ScanErrors = [] }, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        factory.DidNotReceive().Create(Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task StartAsync_WithNullScanErrors_DoesNotInvokeSyncActions()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        var factory = Substitute.For<ISyncActionsServerFactory>();
        var service = CreateService(scenario, factory);

        // Act
        var result = await service.StartAsync(device.Id, scenario.AdminUser.Id,
            new SyncStartInput { ScanErrors = null }, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        factory.DidNotReceive().Create(Arg.Any<MusicDbContext>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<bool>());
    }
}