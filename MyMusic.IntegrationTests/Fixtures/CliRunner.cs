using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using MyMusic.OpenTelemetry.XUnit;

namespace MyMusic.IntegrationTests.Fixtures;

public class CliRunner(IConfiguration configuration, IntegrationTestTelemetry telemetry)
{
    private static string DefaultCliPath() => Path.Combine(
        FindSolutionRoot(),
        "MyMusic.CLI",
        "bin",
        "Debug",
        "net10.0",
        "my-music");

    private static readonly string CliPath =
        Environment.GetEnvironmentVariable("CLI_PATH") is { } envPath && !string.IsNullOrEmpty(envPath)
            ? envPath
            : DefaultCliPath();

    public Task<CliResult> SyncAsync(
        CliTestFixture fixture,
        bool force = false,
        bool autoConfirm = true,
        bool dryRun = false,
        SyncDirection? direction = null,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "sync" };

        if (force) args.Add("--force");
        if (dryRun) args.Add("--dry-run");
        if (direction is not null && direction != SyncDirection.Both)
        {
            args.Add("--direction");
            args.Add(direction.Value.ToString().ToLowerInvariant());
        }
        if (autoConfirm) args.Add("--yes");

        return RunAsync(fixture, args.ToArray(), cancellationToken);
    }

    private async Task<CliResult> RunAsync(
        CliTestFixture fixture,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        // Spectre.Console requires global options AFTER the command name, not before.
        // E.g., "sync --loglevel Debug --verbose --yes" is valid, but
        // "--loglevel Debug --verbose sync --yes" causes CommandParseException.
        var globalOptions = new[] { "--loglevel", "Debug", "--verbose" };
        var allArgs = args.Take(1).Concat(globalOptions).Concat(args.Skip(1));
        var argsString = string.Join(" ", allArgs);

        using var span = telemetry.StartProcessSpan("my-music", argsString);

        var traceparent = Activity.Current?.Id;

        var startInfo = new ProcessStartInfo
        {
            FileName = CliPath,
            Arguments = argsString,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = fixture.RepositoryPath,
        };

        startInfo.Environment["MYMUSIC_CONFIG_PATH"] = fixture.ConfigPath;

        if (!string.IsNullOrEmpty(traceparent))
        {
            startInfo.Environment["OTEL_TRACE_PARENT"] = traceparent;
        }

        ForwardOpenTelemetryConfig(startInfo);

        using var process = new Process();
        process.StartInfo = startInfo;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        span?.SetTag("exit_code", process.ExitCode);
        span?.Stop();

        return new CliResult(
            process.ExitCode,
            stdout.ToString(),
            stderr.ToString());
    }

    private void ForwardOpenTelemetryConfig(ProcessStartInfo startInfo)
    {
        var otelConfigType = Type.GetType("MyMusic.OpenTelemetry.OtelConfig, MyMusic.OpenTelemetry")!;
        var config = configuration.GetSection("OpenTelemetry").Get(otelConfigType);
        if (config is null)
        {
            return;
        }

        foreach (var prop in otelConfigType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(config);
            if (value is null || (value is string s && string.IsNullOrEmpty(s)))
            {
                continue;
            }

            var envKey = $"OpenTelemetry__{prop.Name}";
            startInfo.Environment[envKey] = value.ToString()!;
        }
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
