using System.Text.Json;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class CliTestFixture : IAsyncDisposable
{
    private readonly string _deviceName;
    private readonly string _tempDir;

    public string RepositoryPath { get; }
    public string ConfigPath { get; }
    public long DeviceId { get; private set; }
    public string ConfigDirectory { get; }
    public string DeviceName => _deviceName;

    public CliTestFixture(string? deviceName = null)
    {
        _deviceName = deviceName ?? $"TestDevice-{Guid.NewGuid():N}";
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
        serverUrl ??= IntegrationTestBase.BaseUrl;

        var deviceResponse = await api.PostAsync("/api/devices", new()
        {
            DataObject = new
            {
                name = _deviceName,
                icon = "IconDeviceLaptop",
                color = "#3B82F6",
            },
        });

        deviceResponse.Ok.ShouldBeTrue();
        var deviceData = await deviceResponse.JsonAsync();
        DeviceId = deviceData!.Value.GetProperty("device").GetProperty("id").GetInt64();

        await WriteConfigAsync(serverUrl, userId, userName);
    }

    public async Task<string> CreateSongAsync(SampleSong song)
    {
        var fileName = $"{SanitizeFileName(song.Title)}.mp3";
        var filePath = Path.Combine(RepositoryPath, fileName);

        var bytes = TestFiles.CreateTestMusicFile(
            song.Title,
            song.Album,
            song.Artists,
            song.Genres,
            song.Year);

        await File.WriteAllBytesAsync(filePath, bytes);
        return filePath;
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
