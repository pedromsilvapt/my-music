using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyMusic.Common.AudioIntegrity;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.AudioIntegrity;

public class AudioIntegrityServiceSpecs
{
    [Fact]
    public async Task ValidateAsync_FilePath_Mp3_DelegatesToValidator()
    {
        // Arrange
        var validator = Substitute.For<IAudioIntegrityValidator>();
        validator.Supports(AudioFormat.Mp3).Returns(true);
        validator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AudioIntegrityReport
            {
                FilePath = "/music/test.mp3",
                Status = AudioIntegrityStatus.Clean,
                Format = AudioFormat.Mp3,
                Strategy = ValidationStrategy.Heuristic,
            }));

        var service = new AudioIntegrityService(
            Options.Create(new AudioIntegrityConfig()),
            [validator],
            NullLogger<AudioIntegrityService>.Instance);

        // Act
        var report = await service.ValidateAsync("/music/test.mp3");

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        await validator.Received(1).ValidateAsync("/music/test.mp3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_Buffer_Mp3_DelegatesToValidator()
    {
        // Arrange
        var validator = Substitute.For<IAudioIntegrityValidator>();
        validator.Supports(AudioFormat.Mp3).Returns(true);
        validator.ValidateAsync(Arg.Any<ReadOnlyMemory<byte>>(), AudioFormat.Mp3, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AudioIntegrityReport
            {
                FilePath = "(buffer)",
                Status = AudioIntegrityStatus.Clean,
                Format = AudioFormat.Mp3,
                Strategy = ValidationStrategy.Heuristic,
            }));

        var service = new AudioIntegrityService(
            Options.Create(new AudioIntegrityConfig()),
            [validator],
            NullLogger<AudioIntegrityService>.Instance);
        var buffer = new byte[] { 0xFF, 0xFB, 0x90, 0x00 };

        // Act
        var report = await service.ValidateAsync(buffer.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        await validator.Received(1).ValidateAsync(Arg.Any<ReadOnlyMemory<byte>>(), AudioFormat.Mp3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_FilePath_UnsupportedExtension_ThrowsNotSupportedException()
    {
        // Arrange
        var service = new AudioIntegrityService(
            Options.Create(new AudioIntegrityConfig()),
            [],
            NullLogger<AudioIntegrityService>.Instance);

        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(() => service.ValidateAsync("/music/test.flac"));
    }

    [Fact]
    public async Task ValidateAsync_Buffer_NoValidatorForFormat_ThrowsNotSupportedException()
    {
        // Arrange
        var service = new AudioIntegrityService(
            Options.Create(new AudioIntegrityConfig()),
            [],
            NullLogger<AudioIntegrityService>.Instance);
        var buffer = new byte[] { 0xFF, 0xFB, 0x90, 0x00 };

        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(() => service.ValidateAsync(buffer.AsMemory(), AudioFormat.Mp3));
    }
}
