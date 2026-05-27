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
}