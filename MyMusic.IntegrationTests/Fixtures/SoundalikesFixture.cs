using System.Text.Json;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

/// <summary>
/// Detects the groups of soundalike songs among the songs already seeded. Every test song has the same audio under
/// different tags, so any two of them are soundalikes.
/// </summary>
public class SoundalikesFixture
{
    private const long SoundalikeAuditRuleId = 9;

    /// <summary>
    /// Scans the user's songs for soundalikes, and returns how many groups were found.
    /// </summary>
    public async Task<int> SeedAsync(IAPIRequestContext api)
    {
        var groupsCount = await ScanAsync(api);
        groupsCount.ShouldBeGreaterThan(0, "The scan should find at least one group of soundalikes");

        return groupsCount;
    }

    /// <summary>
    /// Scans the user's songs for soundalikes again, and returns how many new groups were found (possibly none).
    /// </summary>
    public async Task<int> ScanAsync(IAPIRequestContext api)
    {
        var response = await api.PostWithTraceAsync($"/api/audits/rules/{SoundalikeAuditRuleId}/scan");
        response.Ok.ShouldBeTrue($"Failed to scan for soundalikes: {response.Status} {response.StatusText}");

        var result = JsonSerializer.Deserialize<ScanResponse>(await response.BodyAsync(), JsonSerializerOptions.Web)!;

        return result.NonConformitiesCreated;
    }

    private record ScanResponse(int NonConformitiesCreated);
}
