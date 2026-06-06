using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using MyMusic.Common.Targets;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.OpenTelemetry.XUnit;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class MobileCliApplication : ISyncApplication
{
    private readonly IConfiguration _configuration;
    private readonly IntegrationTestTelemetry _telemetry;

    private string _tempDir = null!;
    private string _repoPath = null!;
    private string _configPath = null!;
    private IAPIRequestContext _api = null!;

    public long DeviceId { get; private set; }
    public string DeviceName { get; private set; } = null!;

    public MobileCliApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry)
    {
        _configuration = configuration;
        _telemetry = telemetry;
        DeviceName = $"TestDevice-{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync(IAPIRequestContext api, long userId, string userName, string? serverUrl = null)
    {
        _api = api;
        serverUrl ??= IntegrationTestBase.BaseUrl;

        _tempDir = Path.Combine(Path.GetTempPath(), $"mymusic-mobile-test-{Guid.NewGuid():N}");
        _repoPath = Path.Combine(_tempDir, "repository");
        _configPath = Path.Combine(_tempDir, "config.json");

        Directory.CreateDirectory(_repoPath);

        // Create device via API
        var deviceResponse = await _api.PostWithTraceAsync("/api/devices", new()
        {
            DataObject = new
            {
                name = DeviceName,
                icon = "IconDeviceMobile",
                color = "#10B981",
            },
        });

        deviceResponse.Ok.ShouldBeTrue();
        var deviceData = await deviceResponse.JsonAsync();
        DeviceId = deviceData!.Value.GetProperty("device").GetProperty("id").GetInt64();

        await WriteConfigAsync(serverUrl, userId, userName);
    }

    public async Task<string> CreateSongAsync(SampleSong song, string? relativePath = null, int? contentVariant = null)
    {
        relativePath ??= song.Title is not null ? $"{SanitizeFileName(song.Title)}.mp3" : $"untitled_{Guid.NewGuid():N}.mp3";
        var filePath = Path.Combine(_repoPath, relativePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bytes = TestFiles.CreateTestMusicFile(song, contentVariant);

        await File.WriteAllBytesAsync(filePath, bytes);
        return relativePath;
    }

    public async Task<List<string>> CreateSongsAsync(params (SampleSong Song, string Path)[] songs)
    {
        var paths = new List<string>();
        foreach (var (song, path) in songs)
        {
            paths.Add(await CreateSongAsync(song, path));
        }
        return paths;
    }

    public bool FileExists(string relativePath)
    {
        return File.Exists(Path.Combine(_repoPath, relativePath));
    }

    public string GetSongPath(string relativePath)
    {
        if (!relativePath.EndsWith(".mp3"))
        {
            relativePath += ".mp3";
        }
        return Path.Combine(_repoPath, relativePath);
    }

    public async Task SetNamingTemplateAsync(string namingTemplate)
    {
        // Update the server device naming template
        var response = await _api.PutWithTraceAsync($"/api/devices/{DeviceId}", new()
        {
            DataObject = new
            {
                namingTemplate,
            },
        });

        response.Ok.ShouldBeTrue();
    }

    public async Task UpdateLocalFileMetadataAsync(string fileName, EditSongOptions options)
    {
        var filePath = GetSongPath(fileName);
        using var tfile = TagLib.File.Create(filePath);

        if (options.Title is not null)
        {
            tfile.Tag.Title = options.Title;
        }

        if (options.Year is not null)
        {
            tfile.Tag.Year = (uint)options.Year.Value;
        }

        if (options.Explicit is not null)
        {
            tfile.Tag.Comment = options.Explicit.Value ? "Explicit" : "";
        }

        // Rebuild tags same way server does to ensure identical checksums
        FileTarget.RebuildTags(tfile);
        tfile.Save();

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);

        await Task.CompletedTask;
    }

    public List<string> GetAllFiles()
    {
        return Directory.GetFiles(_repoPath, "*.mp3", SearchOption.AllDirectories)
            .Select(p => p[(_repoPath.Length + 1)..].Replace('\\', '/'))
            .OrderBy(p => p)
            .ToList();
    }

    public void FileShouldExist(string relativePath, string? message = null)
    {
        var files = GetAllFiles();
        var normalizedPath = relativePath.Replace('\\', '/');
        files.ShouldContain(normalizedPath, message ?? $"File should exist: {normalizedPath}");
    }

    public void FilesShouldExist(IEnumerable<string> relativePaths, string? message = null)
    {
        var files = GetAllFiles();
        var normalizedPaths = relativePaths.Select(p => p.Replace('\\', '/')).ToList();
        foreach (var path in normalizedPaths)
        {
            files.ShouldContain(path, message ?? $"File should exist: {path}");
        }
    }

    public void FileShouldNotExist(string relativePath, string? message = null)
    {
        var files = GetAllFiles();
        var normalizedPath = relativePath.Replace('\\', '/');
        files.ShouldNotContain(normalizedPath, message ?? $"File should not exist: {normalizedPath}");
    }

    public async Task<SyncResult> SyncAsync(SyncOptions options)
    {
        var args = new List<string> { "sync" };
        if (options.Force) args.Add("--force");
        if (options.DryRun) args.Add("--dry-run");
        if (options.AutoConfirm) args.Add("--yes");
        if (options.Direction is not null)
        {
            args.Add("--direction");
            args.Add(options.Direction.Value.ToString().ToLowerInvariant());
        }

        var cliPath = FindCliPath();
        var argsString = string.Join(" ", args);

        using var span = _telemetry.StartProcessSpan("mymusic-mobile-sync", argsString);

        var traceparent = Activity.Current?.Id;

        // Find tsx relative to the CLI path
        var cliDir = Path.GetDirectoryName(cliPath) ?? "";
        var tsxBin = Path.Combine(cliDir, "..", "node_modules", ".bin", "tsx");
        var tsxBinResolved = Path.GetFullPath(tsxBin);

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{tsxBinResolved}\" {cliPath} {argsString}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _repoPath,
        };

        startInfo.Environment["MYMUSIC_CONFIG_PATH"] = _configPath;

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

        await process.WaitForExitAsync();

        span?.SetTag("exit_code", process.ExitCode);
        span?.Stop();

        var result = SyncResult.ParseCliOutput(process.ExitCode, stdout.ToString());

        var apiRecordCounts = await SessionRecordHelper.FetchApiRecordCountsAsync(
            _api, DeviceId, result.SessionId);

        return result with { ApiRecordCounts = apiRecordCounts };
    }

    public bool SupportsSyncDirection() => false;

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        await ValueTask.CompletedTask;
    }

    private async Task WriteConfigAsync(string serverUrl, long userId, string userName)
    {
        var config = new
        {
            deviceId = DeviceId,
            repositoryPath = _repoPath,
            serverUrl,
            userId,
            userName,
            musicExtensions = new[] { ".mp3" },
            excludePatterns = new[] { "**/.*", "**/Thumbs.db" },
            chunkSize = 50,
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        await File.WriteAllTextAsync(_configPath, json);
    }

    private static string FindCliPath()
    {
        // Check environment variable first (for containerized tests)
        var envPath = Environment.GetEnvironmentVariable("MOBILE_CLI_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
        {
            return envPath;
        }
        
        // Fallback to solution root discovery (for local development)
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "MyMusic.sln")))
            {
                return Path.Combine(dir, "MyMusic.Mobile", "test-cli", "sync-cli.ts");
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new Exception("Could not find Mobile CLI. Set MOBILE_CLI_PATH environment variable.");
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

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}
