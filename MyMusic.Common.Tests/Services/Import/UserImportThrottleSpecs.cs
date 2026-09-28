using Microsoft.Extensions.Options;
using MyMusic.Common.Services;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Import;

public class UserImportThrottleSpecs
{
    private static UserImportThrottle CreateThrottle(int maxConcurrentImportsPerUser) =>
        new(Options.Create(new Config
        {
            MusicRepositoryPath = "/data",
            MaxConcurrentImportsPerUser = maxConcurrentImportsPerUser,
        }));

    [Fact]
    public async Task Acquire_UserAtLimit_WaitsUntilASlotIsFreed()
    {
        var throttle = CreateThrottle(2);

        // The user fills both slots
        var first = await throttle.AcquireAsync(1, TestContext.Current.CancellationToken);
        using var second = await throttle.AcquireAsync(1, TestContext.Current.CancellationToken);

        // A third import should wait
        var third = throttle.AcquireAsync(1, TestContext.Current.CancellationToken);
        third.IsCompleted.ShouldBeFalse();

        // Freeing a slot should let it in
        first.Dispose();
        using var thirdSlot = await third.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Acquire_OtherUserAtLimit_DoesNotWait()
    {
        var throttle = CreateThrottle(1);

        using var firstUserSlot = await throttle.AcquireAsync(1, TestContext.Current.CancellationToken);

        // Another user's imports should not be limited by the first user's
        var secondUser = throttle.AcquireAsync(2, TestContext.Current.CancellationToken);
        secondUser.IsCompleted.ShouldBeTrue();
        (await secondUser).Dispose();
    }

    [Fact]
    public async Task Dispose_Twice_FreesOnlyOneSlot()
    {
        var throttle = CreateThrottle(1);

        var slot = await throttle.AcquireAsync(1, TestContext.Current.CancellationToken);
        slot.Dispose();
        slot.Dispose();

        // Only one slot exists: after taking it, the next import should still wait
        using var taken = await throttle.AcquireAsync(1, TestContext.Current.CancellationToken);
        throttle.AcquireAsync(1, TestContext.Current.CancellationToken).IsCompleted.ShouldBeFalse();
    }
}
