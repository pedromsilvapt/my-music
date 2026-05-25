using System.Text.Json;
using Microsoft.Playwright;
using MyMusic.Common.Targets;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class DesktopCliFixture : IAsyncDisposable
{
    private readonly string _deviceName;
    private readonly string? _namingTemplate;
    private readonly string _tempDir;
    private IAPIRequestContext _api = null!;

    public string RepositoryPath { get; }
    public string ConfigPath { get; }
    public long DeviceId { get; private set; }
    public string ConfigDirectory { get; }
    public string DeviceName => _deviceName;

    public DesktopCliFixture(string? deviceName = null, string? namingTemplate = null)
    {
        _deviceName = deviceName ?? $"TestDevice-{Guid.NewGuid():N}";
        _namingTemplate = namingTemplate;
        _tempDir = Path.Combine(Path.GetTempPath(), $"mymusic-cli-test-{Guid.NewGuid():N}");

        RepositoryPath = Path.Combine(_tempDir, "repository");
        ConfigDirectory = Path.Combine(_tempDir, "config");
        ConfigPath = Path.Combine(ConfigDirectory, "appsettings.json");

        Directory.CreateDirectory(RepositoryPath);
        Directory.CreateDirectory(ConfigDirectory);
    }

    public async Task InitializeAsync(
        IAPIRequestContext api,
        long userId,
        string userName,
        string? serverUrl = null)
    {
        _api = api;
        serverUrl ??= IntegrationTestBase.BaseUrl;

        var deviceResponse = await _api.PostWithTraceAsync("/api/devices", new()
        {
            DataObject = new
            {
                name = _deviceName,
                icon = "IconDeviceLaptop",
                color = "#3B82F6",
                namingTemplate = _namingTemplate,
            },
        });

        deviceResponse.Ok.ShouldBeTrue();
        var deviceData = await deviceResponse.JsonAsync();
        DeviceId = deviceData!.Value.GetProperty("device").GetProperty("id").GetInt64();

        await WriteConfigAsync(serverUrl, userId, userName);
    }

    public async Task SetNamingTemplateAsync(string namingTemplate)
    {
        // Update the server device
        var response = await _api.PutWithTraceAsync($"/api/devices/{DeviceId}", new()
        {
            DataObject = new
            {
                namingTemplate,
            },
        });

        response.Ok.ShouldBeTrue();

        // Update the local CLI config to match
        await UpdateConfigNamingTemplateAsync(namingTemplate);
    }

    private async Task UpdateConfigNamingTemplateAsync(string namingTemplate)
    {
        // Read existing config
        var json = await File.ReadAllTextAsync(ConfigPath);
        var config = JsonSerializer.Deserialize<JsonElement>(json);

        // Build updated config with new naming template
        var updatedConfig = new
        {
            MyMusic = new
            {
                Server = new
                {
                    BaseUrl = config.GetProperty("myMusic").GetProperty("server").GetProperty("baseUrl").GetString(),
                    UserId = config.GetProperty("myMusic").GetProperty("server").GetProperty("userId").GetInt64(),
                    UserName = config.GetProperty("myMusic").GetProperty("server").GetProperty("userName").GetString(),
                },
                Device = new
                {
                    Name = _deviceName,
                    Icon = "IconDeviceLaptop",
                    Color = "#3B82F6",
                    NamingTemplate = namingTemplate,
                },
                Repository = new
                {
                    Path = RepositoryPath,
                    ExcludePatterns = new[] { "**/.*", "**/Thumbs.db" },
                    MusicExtensions = new[] { ".mp3" },
                },
                Sync = new
                {
                    ChunkSize = 50,
                },
                Logging = new
                {
                    EnableFileLogging = false,
                },
            },
        };

        var updatedJson = JsonSerializer.Serialize(updatedConfig, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        await File.WriteAllTextAsync(ConfigPath, updatedJson);
    }

    public async Task<string> CreateSongAsync(SampleSong song, string? relativePath = null, int? contentVariant = null)
    {
        relativePath ??= song.Title is not null ? $"{SanitizeFileName(song.Title)}.mp3" : $"untitled_{Guid.NewGuid():N}.mp3";
        var filePath = Path.Combine(RepositoryPath, relativePath);

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

    public string GetSongPath(string fileName)
    {
        if (!fileName.EndsWith(".mp3"))
        {
            fileName += ".mp3";
        }
        return Path.Combine(RepositoryPath, fileName);
    }

    public bool FileExists(string relativePath)
    {
        return File.Exists(Path.Combine(RepositoryPath, relativePath));
    }

    /// <summary>Gets all MP3 files relative to RepositoryPath with forward slashes.</summary>
    public List<string> GetAllFiles()
    {
        return Directory.GetFiles(RepositoryPath, "*.mp3", SearchOption.AllDirectories)
            .Select(p => p[(RepositoryPath.Length + 1)..].Replace('\\', '/'))
            .OrderBy(p => p)
            .ToList();
    }

    /// <summary>Asserts that a file exists at the given relative path.</summary>
    public void FileShouldExist(string relativePath, string? message = null)
    {
        var files = GetAllFiles();
        var normalizedPath = relativePath.Replace('\\', '/');
        files.ShouldContain(normalizedPath, message ?? $"File should exist: {normalizedPath}");
    }

    /// <summary>Asserts that all files exist at the given relative paths.</summary>
    public void FilesShouldExist(IEnumerable<string> relativePaths, string? message = null)
    {
        var files = GetAllFiles();
        var normalizedPaths = relativePaths.Select(p => p.Replace('\\', '/')).ToList();
        foreach (var path in normalizedPaths)
        {
            files.ShouldContain(path, message ?? $"File should exist: {path}");
        }
    }

    /// <summary>Asserts that a file does NOT exist at the given relative path.</summary>
    public void FileShouldNotExist(string relativePath, string? message = null)
    {
        var files = GetAllFiles();
        var normalizedPath = relativePath.Replace('\\', '/');
        files.ShouldNotContain(normalizedPath, message ?? $"File should not exist: {normalizedPath}");
    }

    public string? FindFileWithPattern(string baseTitle, string suffix)
    {
        var files = Directory.GetFiles(RepositoryPath, "*.mp3");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Contains(baseTitle) && fileName.Contains(suffix))
            {
                return file;
            }
        }
        return null;
    }

    private async Task WriteConfigAsync(string serverUrl, long userId, string userName)
    {
        var config = new
        {
            MyMusic = new
            {
                Server = new
                {
                    BaseUrl = serverUrl,
                    UserId = userId,
                    UserName = userName,
                },
                Device = new
                {
                    Name = _deviceName,
                    Icon = "IconDeviceLaptop",
                    Color = "#3B82F6",
                    NamingTemplate = _namingTemplate,
                },
                Repository = new
                {
                    Path = RepositoryPath,
                    ExcludePatterns = new[] { "**/.*", "**/Thumbs.db" },
                    MusicExtensions = new[] { ".mp3" },
                },
                Sync = new
                {
                    ChunkSize = 50,
                },
                Logging = new
                {
                    EnableFileLogging = false,
                },
            },
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        await File.WriteAllTextAsync(ConfigPath, json);
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

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        await ValueTask.CompletedTask;
    }
}
