using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.OpenTelemetry.XUnit;

namespace MyMusic.IntegrationTests.Fixtures;

public class DesktopCliApplication : ISyncApplication
{
    private readonly DesktopCliFixture _fixture;
    private readonly IConfiguration _configuration;
    private readonly IntegrationTestTelemetry _telemetry;

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

    public long DeviceId => _fixture.DeviceId;
    public string DeviceName => _fixture.DeviceName;

    public DesktopCliApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry)
    {
        _fixture = new DesktopCliFixture();
        _configuration = configuration;
        _telemetry = telemetry;
    }

    public DesktopCliApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry, string? deviceName = null, string? namingTemplate = null)
    {
        _fixture = new DesktopCliFixture(deviceName, namingTemplate);
        _configuration = configuration;
        _telemetry = telemetry;
    }

    public Task InitializeAsync(IAPIRequestContext api, long userId, string userName, string? serverUrl = null)
        => _fixture.InitializeAsync(api, userId, userName, serverUrl);

    public Task<string> CreateSongAsync(SampleSong song, string? relativePath = null, int? contentVariant = null)
        => _fixture.CreateSongAsync(song, relativePath, contentVariant);

    public Task<List<string>> CreateSongsAsync(params (SampleSong Song, string Path)[] songs)
        => _fixture.CreateSongsAsync(songs);

    public bool FileExists(string relativePath)
        => _fixture.FileExists(relativePath);

    public string GetSongPath(string relativePath)
        => _fixture.GetSongPath(relativePath);

    public Task SetNamingTemplateAsync(string namingTemplate)
        => _fixture.SetNamingTemplateAsync(namingTemplate);

    public Task UpdateLocalFileMetadataAsync(string fileName, EditSongOptions options)
        => _fixture.UpdateLocalFileMetadataAsync(fileName, options);

    public List<string> GetAllFiles()
        => _fixture.GetAllFiles();

    public void FileShouldExist(string relativePath, string? message = null)
        => _fixture.FileShouldExist(relativePath, message);

    public void FilesShouldExist(IEnumerable<string> relativePaths, string? message = null)
        => _fixture.FilesShouldExist(relativePaths, message);

    public void FileShouldNotExist(string relativePath, string? message = null)
        => _fixture.FileShouldNotExist(relativePath, message);

    public async Task<SyncResult> SyncAsync(SyncOptions options)
    {
        var args = new List<string> { "sync" };

        if (options.Force) args.Add("--force");
        if (options.DryRun) args.Add("--dry-run");
        if (options.Direction is not null && options.Direction != SyncDirection.Both)
        {
            args.Add("--direction");
            args.Add(options.Direction.Value.ToString().ToLowerInvariant());
        }
        if (options.AutoConfirm) args.Add("--yes");

        // Spectre.Console requires global options AFTER the command name, not before.
        // E.g., "sync --loglevel Debug --verbose --yes" is valid, but
        // "--loglevel Debug --verbose sync --yes" causes CommandParseException.
        var globalOptions = new[] { "--loglevel", "Debug", "--verbose" };
        var allArgs = args.Take(1).Concat(globalOptions).Concat(args.Skip(1));
        var argsString = string.Join(" ", allArgs);

        using var span = _telemetry.StartProcessSpan("my-music", argsString);

        var traceparent = Activity.Current?.Id;

        var startInfo = new ProcessStartInfo
        {
            FileName = CliPath,
            Arguments = argsString,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _fixture.RepositoryPath,
        };

        startInfo.Environment["MYMUSIC_CONFIG_PATH"] = _fixture.ConfigPath;

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
                _telemetry.TestsLogger.LogDebug("CLI: " + e.Data);
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                _telemetry.TestsLogger.LogWarning("CLI: " + e.Data);
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        span?.SetTag("exit_code", process.ExitCode);
        span?.Stop();

        return SyncResult.ParseCliOutput(process.ExitCode, stdout.ToString());
    }

    public bool SupportsSyncDirection() => true;

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private void ForwardOpenTelemetryConfig(ProcessStartInfo startInfo)
    {
        var otelConfigType = Type.GetType("MyMusic.OpenTelemetry.OtelConfig, MyMusic.OpenTelemetry")!;
        var config = _configuration.GetSection("OpenTelemetry").Get(otelConfigType);
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
