using MyMusic.IntegrationTests.Fixtures;
using Shouldly;

namespace MyMusic.IntegrationTests.Extensions;

public static class SyncResultShouldlyExtensions
{
    public static void ShouldBeSuccessful(this SyncResult result)
    {
        result.Success.ShouldBeTrue($"Sync failed (Success=false)");
    }

    public static void ShouldBe(
        this SyncResult result,
        bool successful = true,
        int? createRemote = null,
        int? updateRemote = null,
        int? createLocal = null,
        int? updateLocal = null,
        int? delete = null,
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
            result.Success.ShouldBeTrue($"Sync failed (Success=false)");
        }

        AssertCounter(nameof(result.CreateRemote), result.CreateRemote, createRemote, result.ApiRecordCounts);
        AssertCounter(nameof(result.UpdateRemote), result.UpdateRemote, updateRemote, result.ApiRecordCounts);
        AssertCounter(nameof(result.CreateLocal), result.CreateLocal, createLocal, result.ApiRecordCounts);
        AssertCounter(nameof(result.UpdateLocal), result.UpdateLocal, updateLocal, result.ApiRecordCounts);
        AssertCounter(nameof(result.Delete), result.Delete, delete, result.ApiRecordCounts);
        AssertCounter(nameof(result.Link), result.Link, link, result.ApiRecordCounts);
        AssertCounter(nameof(result.Unlink), result.Unlink, unlink, result.ApiRecordCounts);
        AssertCounter(nameof(result.Rename), result.Rename, rename, result.ApiRecordCounts);
        AssertCounter(nameof(result.Skipped), result.Skipped, skipped, result.ApiRecordCounts);
        AssertCounter(nameof(result.Conflict), result.Conflict, conflict, result.ApiRecordCounts);
        AssertCounter(nameof(result.UpdateTimestamp), result.UpdateTimestamp, updateTimestamp, result.ApiRecordCounts);
        AssertCounter(nameof(result.Error), result.Error, error, result.ApiRecordCounts);
    }

    private static void AssertCounter(
        string actionName,
        int actualCounter,
        int? expected,
        Dictionary<string, int>? apiRecordCounts)
    {
        var expectedValue = expected ?? 0;

        actualCounter.ShouldBe(expectedValue, $"CLI counter {actionName} should be {expectedValue} but was {actualCounter}");

        if (apiRecordCounts is not null)
        {
            var apiCount = apiRecordCounts.GetValueOrDefault(actionName, 0);
            apiCount.ShouldBe(expectedValue, $"API record count for {actionName} should be {expectedValue} but was {apiCount}");
        }
    }
}
