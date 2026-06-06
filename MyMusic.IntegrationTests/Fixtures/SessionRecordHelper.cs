using System.Text.Json;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;

namespace MyMusic.IntegrationTests.Fixtures;

public static class SessionRecordHelper
{
    public static async Task<Dictionary<string, int>?> FetchApiRecordCountsAsync(
        IAPIRequestContext api,
        long deviceId,
        long? sessionId)
    {
        if (sessionId is null)
        {
            return null;
        }

        var response = await api.GetWithTraceAsync($"/api/devices/{deviceId}/sessions/{sessionId}/records");

        if (!response.Ok)
        {
            return null;
        }

        var json = await response.JsonAsync();
        if (json is null)
        {
            return null;
        }

        var recordsElement = json.Value.GetProperty("records");

        var counts = new Dictionary<string, int>();

        foreach (var record in recordsElement.EnumerateArray())
        {
            var action = record.GetProperty("action").GetString()!;
            counts.TryGetValue(action, out var current);
            counts[action] = current + 1;
        }

        return counts;
    }
}