using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyMusic.Common.AudioIntegrity;
using Shouldly;

namespace MyMusic.Common.Tests.AudioIntegrity;

public class FFmpegRunnerSpecs
{
    [Fact]
    public async Task RunAsync_FFmpegNotInstalled_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new AudioIntegrityConfig();
        var runner = new FFmpegRunner(Options.Create(config), NullLogger<FFmpegRunner>.Instance);

        // Point to a non-existent executable
        Environment.SetEnvironmentVariable("FFMPEG_PATH", "/nonexistent/ffmpeg_binary");

        try
        {
            // Act & Assert
            await Should.ThrowAsync<InvalidOperationException>(
                () => runner.RunAsync(null, new byte[] { 0x00 }.AsMemory(), 5, CancellationToken.None));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FFMPEG_PATH", null);
        }
    }

    [Fact]
    public async Task RunAsync_ConfigFFmpegPath_TakesPrecedenceOverEnvironmentVariable()
    {
        // Arrange
        var config = new AudioIntegrityConfig { FFmpegPath = "/config/ffmpeg" };
        var runner = new FFmpegRunner(Options.Create(config), NullLogger<FFmpegRunner>.Instance);

        // Set env var to a different path
        Environment.SetEnvironmentVariable("FFMPEG_PATH", "/env/ffmpeg");

        try
        {
            // Act & Assert - should fail with config path, not env path
            var ex = await Should.ThrowAsync<InvalidOperationException>(
                () => runner.RunAsync(null, new byte[] { 0x00 }.AsMemory(), 5, CancellationToken.None));
            ex.Message.ShouldContain("/config/ffmpeg");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FFMPEG_PATH", null);
        }
    }
}
