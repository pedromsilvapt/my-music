using System.Text.Json;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public static class SessionRecordHelper
{
    /// <summary>
    /// Returns the file paths of the session's records with the given action.
    /// </summary>
    public static async Task<List<string>> FetchRecordPathsAsync(
        IAPIRequestContext api,
        long deviceId,
        long sessionId,
        string action)
    {
        var response = await api.GetWithTraceAsync($"/api/devices/{deviceId}/sessions/{sessionId}/records");
        response.Ok.ShouldBeTrue($"Failed to list session records: {response.Status} {response.StatusText}");

        var json = await response.JsonAsync();

        return json!.Value.GetProperty("records").EnumerateArray()
            .Where(record => record.GetProperty("action").GetString() == action)
            .Select(record => record.GetProperty("filePath").GetString()!)
            .ToList();
    }

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
        var resolvedConflicts = new HashSet<long>();

        foreach (var record in recordsElement.EnumerateArray())
        {
            var action = record.GetProperty("action").GetString()!;
            counts.TryGetValue(action, out var current);
            counts[action] = current + 1;

            if (record.TryGetProperty("resolvesConflictRecordId", out var resolves) && resolves.ValueKind == JsonValueKind.Number)
            {
                resolvedConflicts.Add(resolves.GetInt64());
            }
        }

        // Like the sync counters, only the conflicts no other record resolves are counted
        if (resolvedConflicts.Count > 0)
        {
            counts["Conflict"] = counts.GetValueOrDefault("Conflict") - resolvedConflicts.Count;
        }

        return counts;
    }
}