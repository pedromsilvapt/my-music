namespace MyMusic.CLI.Tests.Services.Sync;

using MyMusic.CLI.Services.Sync;
using Shouldly;
using Xunit;

/// <summary>
/// The same vectors as SyncComparisonHelperSpecs in MyMusic.Common.Tests: the two implementations must agree.
/// </summary>
public class SyncDateComparisonTests
{
    [Fact]
    public void IsNewerThan_CurrentStrictlyNewer_ReturnsTrue()
    {
        var reference = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var current = reference.AddSeconds(1);

        SyncDateComparison.IsNewerThan(current, reference).ShouldBeTrue();
    }

    [Fact]
    public void IsNewerThan_CurrentOlder_ReturnsFalse()
    {
        var reference = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var current = reference.AddSeconds(-1);

        SyncDateComparison.IsNewerThan(current, reference).ShouldBeFalse();
    }

    [Fact]
    public void IsNewerThan_EqualTimestamps_ReturnsFalse()
    {
        var reference = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        SyncDateComparison.IsNewerThan(reference, reference).ShouldBeFalse();
    }

    [Fact]
    public void IsNewerThan_WithinTickPrecision_ReturnsFalse()
    {
        var reference = new DateTime((DateTime.UtcNow.AddHours(-1).Ticks / 10) * 10);
        var current = reference.AddTicks(9);

        SyncDateComparison.IsNewerThan(current, reference).ShouldBeFalse();
    }

    [Fact]
    public void IsNewerThan_BeyondTickPrecision_ReturnsTrue()
    {
        var reference = new DateTime((DateTime.UtcNow.AddHours(-1).Ticks / 10) * 10);
        var current = reference.AddTicks(10);

        SyncDateComparison.IsNewerThan(current, reference).ShouldBeTrue();
    }
}
