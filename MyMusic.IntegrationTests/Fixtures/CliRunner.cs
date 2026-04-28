using System.Diagnostics;
using System.Text;

namespace MyMusic.IntegrationTests.Fixtures;

public static class CliRunner
{
    private static readonly string DefaultCliPath = Path.Combine(
        FindSolutionRoot(),
        "MyMusic.CLI",
        "bin",
        "Debug",
        "net10.0",
        "my-music");

    private static readonly string CliPath =
        Environment.GetEnvironmentVariable("CLI_PATH") is { } envPath && !string.IsNullOrEmpty(envPath)
            ? envPath
            : DefaultCliPath;

    public static Task<CliResult> SyncAsync(
        CliTestFixture fixture,
        bool force = false,
        bool autoConfirm = true,
        bool dryRun = false,
        bool verbose = true,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "sync" };

        if (force) args.Add("--force");
        if (dryRun) args.Add("--dry-run");
        if (autoConfirm) args.Add("--yes");
        if (verbose) args.Add("--verbose");

        return RunAsync(fixture, args.ToArray(), cancellationToken);
    }

    public static async Task<CliResult> RunAsync(
        CliTestFixture fixture,
        params string[] args)
    {
        return await RunAsync(fixture, args, CancellationToken.None);
    }

    private static async Task<CliResult> RunAsync(
        CliTestFixture fixture,
        string[] args,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = CliPath,
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = fixture.RepositoryPath,
        };

        startInfo.Environment["MYMUSIC_CONFIG_PATH"] = fixture.ConfigPath;

        using var process = new Process();
        process.StartInfo = startInfo;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new CliResult(
            process.ExitCode,
            stdout.ToString(),
            stderr.ToString());
    }

    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "MyMusic.sln")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new Exception("Could not find solution root");
    }
}
