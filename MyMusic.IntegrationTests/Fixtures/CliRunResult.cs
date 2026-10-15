namespace MyMusic.IntegrationTests.Fixtures;

/// <summary>
/// What a run of a sync client's CLI (desktop or mobile) exited with and printed.
/// </summary>
public record CliRunResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string DescribeCliOutput() =>
        $"Exit code: {ExitCode}\nStandard output:\n{StandardOutput}\nStandard error:\n{StandardError}";
}
