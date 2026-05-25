using System.Text.RegularExpressions;

namespace MyMusic.IntegrationTests.Fixtures;

public record SyncResult
{
    public bool Success { get; init; }
    public int CreateRemote { get; init; }
    public int UpdateRemote { get; init; }
    public int CreateLocal { get; init; }
    public int UpdateLocal { get; init; }
    public int Delete { get; init; }
    public int Link { get; init; }
    public int Unlink { get; init; }
    public int Rename { get; init; }
    public int Skipped { get; init; }
    public int Conflict { get; init; }
    public int UpdateTimestamp { get; init; }
    public int Error { get; init; }

    public int TotalChanges => CreateRemote + UpdateRemote + CreateLocal + UpdateLocal + Delete + Link + Unlink + Rename;

    public static SyncResult ParseCliOutput(int exitCode, string standardOutput)
    {
        return new SyncResult
        {
            Success = exitCode == 0,
            CreateRemote = GetCounterValue(standardOutput, "CreateRemote"),
            UpdateRemote = GetCounterValue(standardOutput, "UpdateRemote"),
            CreateLocal = GetCounterValue(standardOutput, "CreateLocal"),
            UpdateLocal = GetCounterValue(standardOutput, "UpdateLocal"),
            Delete = GetCounterValue(standardOutput, "Delete"),
            Link = GetCounterValue(standardOutput, "Link"),
            Unlink = GetCounterValue(standardOutput, "Unlink"),
            Rename = GetCounterValue(standardOutput, "Rename"),
            Skipped = GetCounterValue(standardOutput, "Skipped"),
            Conflict = GetCounterValue(standardOutput, "Conflict"),
            UpdateTimestamp = GetCounterValue(standardOutput, "UpdateTimestamp"),
            Error = GetCounterValue(standardOutput, "Error"),
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
