using System.Text.RegularExpressions;

namespace MyMusic.IntegrationTests.Fixtures;

public record SyncResult
{
    public bool Success { get; init; }
    public long? SessionId { get; init; }
    public int CreateRemote { get; init; }
    public int UpdateRemote { get; init; }
    public int CreateLocal { get; init; }
    public int UpdateLocal { get; init; }
    public int DeleteLocal { get; init; }
    public int Link { get; init; }
    public int Unlink { get; init; }
    public int Rename { get; init; }
    public int Skipped { get; init; }
    public int Conflict { get; init; }
    public int UpdateTimestamp { get; init; }
    public int Error { get; init; }
    public Dictionary<string, int>? ApiRecordCounts { get; init; }

    /// <summary>
    /// Whether the sync kept its Skipped records. When false they are counted but not stored.
    /// </summary>
    public bool RecordSkipped { get; init; } = true;

    /// <summary>
    /// Value of a counter that could not be found in the CLI's standard output.
    /// </summary>
    public const int MissingCounter = -1;

    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";

    public int TotalChanges => CreateRemote + UpdateRemote + CreateLocal + UpdateLocal + DeleteLocal + Link + Unlink + Rename;

    public static SyncResult ParseCliOutput(int exitCode, string standardOutput, string standardError)
    {
        return new SyncResult
        {
            Success = exitCode == 0,
            ExitCode = exitCode,
            StandardOutput = standardOutput,
            StandardError = standardError,
            SessionId = GetNullableLongValue(standardOutput, "SessionId"),
            CreateRemote = GetCounterValue(standardOutput, "CreateRemote"),
            UpdateRemote = GetCounterValue(standardOutput, "UpdateRemote"),
            CreateLocal = GetCounterValue(standardOutput, "CreateLocal"),
            UpdateLocal = GetCounterValue(standardOutput, "UpdateLocal"),
            DeleteLocal = GetCounterValue(standardOutput, "DeleteLocal"),
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
        if (matches.Count == 0) return MissingCounter;
        return int.Parse(matches[^1].Groups[1].Value);
    }

    /// <summary>
    /// Describes what the CLI process returned and printed, to diagnose a sync whose output was not the expected one.
    /// </summary>
    public string DescribeCliOutput() =>
        $"CLI exited with code {ExitCode}\n--- stdout ---\n{StandardOutput}\n--- stderr ---\n{StandardError}";

    private static long? GetNullableLongValue(string output, string name)
    {
        var strippedOutput = Regex.Replace(output, @"\x1b\[[0-9;]*m", "");
        var matches = Regex.Matches(strippedOutput, $@"{name}:\s*(\d+)");
        if (matches.Count == 0) return null;
        return long.Parse(matches[^1].Groups[1].Value);
    }
}