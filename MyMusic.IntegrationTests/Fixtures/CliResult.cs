using System.Text.RegularExpressions;

namespace MyMusic.IntegrationTests.Fixtures;

public record CliResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;

    public int Created => GetCounterValue("Created");
    public int Updated => GetCounterValue("Updated");
    public int Downloaded => GetCounterValue("Downloaded");
    public int Removed => GetCounterValue("Removed");
    public int Failed => GetCounterValue("Failed");
    public int Conflicts => GetCounterValue("Conflicts");

    public int TotalChanges => Created + Updated + Downloaded + Removed;

    private int GetCounterValue(string counterName)
    {
        var strippedOutput = Regex.Replace(StandardOutput, @"\x1b\[[0-9;]*m", "");
        var matches = Regex.Matches(strippedOutput, $@"{counterName}:\s*(\d+)");
        if (matches.Count == 0) return -1;
        return int.Parse(matches[^1].Groups[1].Value);
    }
}
