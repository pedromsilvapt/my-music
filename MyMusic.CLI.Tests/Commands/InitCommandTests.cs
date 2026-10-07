namespace MyMusic.CLI.Tests.Commands;

using MyMusic.CLI.Commands;
using Shouldly;
using Xunit;

public class InitCommandTests
{
    [Fact]
    public void GetConfigPath_WithEnvVar_ReturnsEnvVarPath()
    {
        var original = Environment.GetEnvironmentVariable("MYMUSIC_CONFIG_PATH");
        try
        {
            Environment.SetEnvironmentVariable("MYMUSIC_CONFIG_PATH", "/custom/path/appsettings.json");
            var result = InitCommand.GetConfigPath();
            result.ShouldBe("/custom/path/appsettings.json");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYMUSIC_CONFIG_PATH", original);
        }
    }

    [Fact]
    public void GetConfigPath_WithoutEnvVar_ReturnsAppDataPath()
    {
        var original = Environment.GetEnvironmentVariable("MYMUSIC_CONFIG_PATH");
        try
        {
            Environment.SetEnvironmentVariable("MYMUSIC_CONFIG_PATH", null);
            var result = InitCommand.GetConfigPath();
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "my-music",
                "appsettings.json");
            result.ShouldBe(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYMUSIC_CONFIG_PATH", original);
        }
    }

    [Fact]
    public void GetConfigPath_WithEmptyEnvVar_ReturnsAppDataPath()
    {
        var original = Environment.GetEnvironmentVariable("MYMUSIC_CONFIG_PATH");
        try
        {
            Environment.SetEnvironmentVariable("MYMUSIC_CONFIG_PATH", "");
            var result = InitCommand.GetConfigPath();
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "my-music",
                "appsettings.json");
            result.ShouldBe(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYMUSIC_CONFIG_PATH", original);
        }
    }

    [Fact]
    public void WriteConfig_WritesExpectedJson()
    {
        using var file = new TempConfigFile();

        InitCommand.WriteConfig(file.Path, "http://localhost:5000", "alice", "Alice's <PC>", "Laptop", true, "/music");

        File.ReadAllText(file.Path).ReplaceLineEndings("\n").ShouldBe(
            """
            {
              "MyMusic": {
                "Server": {
                  "BaseUrl": "http://localhost:5000",
                  "UserName": "alice"
                },
                "Device": {
                  "Name": "{ESCAPED_NAME}",
                  "Icon": "IconDeviceLaptop",
                  "ImportOnPurchase": true
                },
                "Repository": {
                  "Path": "/music",
                  "ExcludePatterns": [
                    "**/.*",
                    "**/Thumbs.db",
                    "**/*.tmp",
                    "**/desktop.ini"
                  ],
                  "MusicExtensions": [
                    ".mp3"
                  ]
                },
                "Logging": {
                  "EnableFileLogging": false,
                  "FilePath": "mymusic-cli.log"
                }
              }
            }
            """.Replace("{ESCAPED_NAME}", "Alice\\u0027s \\u003CPC\\u003E").ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WriteConfig_ThenReadExistingConfig_RoundTripsValues()
    {
        using var file = new TempConfigFile();

        InitCommand.WriteConfig(file.Path, "https://music.example.com", "bob", "Bob's Phone", "Smartphone", false, "/sdcard/Music");
        var config = InitCommand.ReadExistingConfig(file.Path);

        config.ShouldBe(new InitCommand.ExistingConfig(
            "https://music.example.com", "bob", "Bob's Phone", "IconDeviceMobile", false, "/sdcard/Music"));
    }

    [Fact]
    public void ReadExistingConfig_MissingFile_ReturnsNull()
    {
        using var file = new TempConfigFile();

        InitCommand.ReadExistingConfig(file.Path).ShouldBeNull();
    }

    [Fact]
    public void ReadExistingConfig_WithoutMyMusicSection_ReturnsNull()
    {
        using var file = new TempConfigFile("""{ "Logging": {} }""");

        InitCommand.ReadExistingConfig(file.Path).ShouldBeNull();
    }

    [Fact]
    public void ReadExistingConfig_PartialConfig_ReturnsAvailableValues()
    {
        using var file = new TempConfigFile("""{ "MyMusic": { "Server": { "BaseUrl": "http://server" } } }""");

        InitCommand.ReadExistingConfig(file.Path).ShouldBe(
            new InitCommand.ExistingConfig("http://server", null, null, null, null, null));
    }

    private sealed class TempConfigFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mymusic-init-{Guid.NewGuid():N}.json");

        public TempConfigFile(string? content = null)
        {
            if (content != null)
            {
                File.WriteAllText(Path, content);
            }
        }

        public void Dispose() => File.Delete(Path);
    }
}