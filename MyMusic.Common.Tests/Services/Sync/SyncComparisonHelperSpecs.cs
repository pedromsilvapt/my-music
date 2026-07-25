using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncComparisonHelperSpecs
{
    private readonly SyncComparisonHelper _helper = new();

    [Fact]
    public void IsNewerThan_CurrentStrictlyNewer_ReturnsTrue()
    {
        var reference = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var current = reference.AddSeconds(1);

        _helper.IsNewerThan(current, reference).ShouldBeTrue();
    }

    [Fact]
    public void IsNewerThan_CurrentOlder_ReturnsFalse()
    {
        var reference = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var current = reference.AddSeconds(-1);

        _helper.IsNewerThan(current, reference).ShouldBeFalse();
    }

    [Fact]
    public void IsNewerThan_EqualTimestamps_ReturnsFalse()
    {
        var reference = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        _helper.IsNewerThan(reference, reference).ShouldBeFalse();
    }

    [Fact]
    public void IsNewerThan_WithinTickPrecision_ReturnsFalse()
    {
        // EF Core/PostgreSQL loses the last digit of tick precision (always 0 when read back),
        // so differences smaller than 10 ticks must not be considered "newer".
        var reference = new DateTime((DateTime.UtcNow.AddHours(-1).Ticks / 10) * 10);
        var current = reference.AddTicks(9);

        _helper.IsNewerThan(current, reference).ShouldBeFalse();
    }

    [Fact]
    public void IsNewerThan_BeyondTickPrecision_ReturnsTrue()
    {
        var reference = new DateTime((DateTime.UtcNow.AddHours(-1).Ticks / 10) * 10);
        var current = reference.AddTicks(10);

        _helper.IsNewerThan(current, reference).ShouldBeTrue();
    }
}