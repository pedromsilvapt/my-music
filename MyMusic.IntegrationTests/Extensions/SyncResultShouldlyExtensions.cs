using MyMusic.IntegrationTests.Fixtures;
using Shouldly;

namespace MyMusic.IntegrationTests.Extensions;

public static class SyncResultShouldlyExtensions
{
    public static void ShouldBeSuccessful(this SyncResult result)
    {
        result.Success.ShouldBeTrue($"Sync failed (Success=false)\n{result.DescribeCliOutput()}");
    }

    public static void ShouldBe(
        this SyncResult result,
        bool successful = true,
        int? createRemote = null,
        int? updateRemote = null,
        int? createLocal = null,
        int? updateLocal = null,
        int? deleteLocal = null,
        int? link = null,
        int? unlink = null,
        int? rename = null,
        int? skipped = null,
        int? conflict = null,
        int? updateTimestamp = null,
        int? error = null)
    {
        if (successful)
        {
            result.Success.ShouldBeTrue($"Sync failed (Success=false)\n{result.DescribeCliOutput()}");
        }

        AssertCounter(result, nameof(result.CreateRemote), result.CreateRemote, createRemote);
        AssertCounter(result, nameof(result.UpdateRemote), result.UpdateRemote, updateRemote);
        AssertCounter(result, nameof(result.CreateLocal), result.CreateLocal, createLocal);
        AssertCounter(result, nameof(result.UpdateLocal), result.UpdateLocal, updateLocal);
        AssertCounter(result, nameof(result.DeleteLocal), result.DeleteLocal, deleteLocal);
        AssertCounter(result, nameof(result.Link), result.Link, link);
        AssertCounter(result, nameof(result.Unlink), result.Unlink, unlink);
        AssertCounter(result, nameof(result.Rename), result.Rename, rename);
        AssertCounter(result, nameof(result.Skipped), result.Skipped, skipped);
        AssertCounter(result, nameof(result.Conflict), result.Conflict, conflict);
        AssertCounter(result, nameof(result.UpdateTimestamp), result.UpdateTimestamp, updateTimestamp);
        AssertCounter(result, nameof(result.Error), result.Error, error);
    }

    private static void AssertCounter(
        SyncResult result,
        string actionName,
        int actualCounter,
        int? expected)
    {
        var expectedValue = expected ?? 0;
        var apiRecordCounts = result.ApiRecordCounts;

        // A missing counter means the CLI's summary was not (fully) captured, so show what was
        if (actualCounter == SyncResult.MissingCounter)
        {
            throw new ShouldAssertException(
                $"CLI counter {actionName} was not found in the CLI output\n{result.DescribeCliOutput()}");
        }

        actualCounter.ShouldBe(expectedValue, $"CLI counter {actionName} should be {expectedValue} but was {actualCounter}");

        if (apiRecordCounts is not null)
        {
            // Skipped records are deleted when the sync completes, unless it was asked to keep them
            var expectedApiCount = actionName == nameof(result.Skipped) && !result.RecordSkipped ? 0 : expectedValue;
            var apiCount = apiRecordCounts.GetValueOrDefault(actionName, 0);
            apiCount.ShouldBe(expectedApiCount, $"API record count for {actionName} should be {expectedApiCount} but was {apiCount}");
        }
    }
}
