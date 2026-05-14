using System.Text.RegularExpressions;

namespace MyMusic.IntegrationTests.Fixtures;

public record SyncResult
{
    public bool Success { get; init; }
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Downloaded { get; init; }
    public int Removed { get; init; }
    public int Failed { get; init; }
    public int Conflicts { get; init; }

    public int TotalChanges => Created + Updated + Downloaded + Removed;

    public static SyncResult ParseCliOutput(int exitCode, string standardOutput)
    {
        return new SyncResult
        {
            Success = exitCode == 0,
            Created = GetCounterValue(standardOutput, "Created"),
            Updated = GetCounterValue(standardOutput, "Updated"),
            Downloaded = GetCounterValue(standardOutput, "Downloaded"),
            Removed = GetCounterValue(standardOutput, "Removed"),
            Failed = GetCounterValue(standardOutput, "Failed"),
            Conflicts = GetCounterValue(standardOutput, "Conflicts"),
        };
    }

    private static int GetCounterValue(string output, string counterName)
    {
        var strippedOutput = Regex.Replace(output, @"\x1b\[[0-9;]*m", "");
        var matches = Regex.Matches(strippedOutput, $@"{counterName}:\s*(\d+)");
        if (matches.Count == 0) return -1;
        return int.Parse(matches[^1].Groups[1].Value);
    }
}
