using MyMusic.IntegrationTests.Fixtures;
using Shouldly;

namespace MyMusic.IntegrationTests.Extensions;

public static class SyncResultShouldlyExtensions
{
    public static void ShouldBeSuccessful(this SyncResult result)
    {
        result.Success.ShouldBeTrue($"Sync failed (Success=false)");
    }
}
