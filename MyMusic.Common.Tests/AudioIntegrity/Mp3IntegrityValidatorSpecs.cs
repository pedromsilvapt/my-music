using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyMusic.Common.AudioIntegrity;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.AudioIntegrity;

public class Mp3IntegrityValidatorSpecs
{
    private static Mp3IntegrityValidator CreateValidator(
        AudioIntegrityConfig? config = null,
        IFFmpegRunner? ffmpegRunner = null,
        IFileSystem? fileSystem = null)
    {
        var cfg = config ?? new AudioIntegrityConfig();
        var runner = ffmpegRunner ?? Substitute.For<IFFmpegRunner>();
        var fs = fileSystem ?? new MockFileSystem();
        return new Mp3IntegrityValidator(
            Options.Create(cfg),
            runner,
            fs,
            NullLogger<Mp3IntegrityValidator>.Instance);
    }

    #region Heuristic Tests

    [Fact]
    public async Task ValidateAsync_CleanFile_ReturnsClean()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateCleanFile(10, bitrateKbps: 128);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        report.ActualFrames.ShouldBe(10);
        report.XingFrames.ShouldBe(10);
        report.FrameDelta.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_OneFrameSurplus_ReturnsClean()
    {
        // Arrange - Xing claims 10 frames, but 11 exist (+1 delta).
        // A 1-frame counting artifact should NOT be flagged; only deltas
        // of 2+ frames are considered suspicious.
        var data = SyntheticMp3Generator.CreateTruncatedFile(
            claimedFrames: 10, actualFrames: 11, bitrateKbps: 128);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        report.FrameDelta.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateAsync_OneFrameMissing_ReturnsClean()
    {
        // Arrange - Xing claims 11 frames, but only 10 exist (-1 delta).
        // A 1-frame counting artifact should NOT be flagged; only missing
        // 2+ frames are considered truncated.
        var data = SyntheticMp3Generator.CreateTruncatedFile(
            claimedFrames: 11, actualFrames: 10, bitrateKbps: 128);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        report.FrameDelta.ShouldBe(-1);
    }

    [Fact]
    public async Task ValidateAsync_ConcatenatedDifferentBitrate_ReturnsSuspect()
    {
        // Arrange - need at least 10 frames in first section for cliff detection
        var data = SyntheticMp3Generator.CreateConcatenatedDifferentBitrate(
            firstFrameCount: 15, firstBitrate: 128,
            secondFrameCount: 5, secondBitrate: 320);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert - frame delta is only 5, so it's Suspect (not Corrupted)
        // Bitrate cliff alone doesn't cause Corrupted anymore
        report.Status.ShouldBe(AudioIntegrityStatus.Suspect);
        report.LargeGap.ShouldBeTrue();
        report.BitrateCliff.ShouldBeTrue();
        report.FirstSectionBitrate.ShouldBe(128);
        report.SecondSectionBitrate.ShouldBe(320);
    }

    [Fact]
    public async Task ValidateAsync_LargeFrameDelta_ReturnsCorrupted()
    {
        // Arrange - Xing claims 10 frames but 120 exist (delta = 110 > 100)
        var data = SyntheticMp3Generator.CreateTruncatedFile(
            claimedFrames: 10, actualFrames: 120, bitrateKbps: 128);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Corrupted);
        report.FrameDelta.ShouldBe(110);
    }

    [Fact]
    public async Task ValidateAsync_ConcatenatedSameBitrate_ReturnsSuspect()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateConcatenatedSameBitrateWithMidFileTag(
            firstFrameCount: 5, secondFrameCount: 5, bitrateKbps: 128);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert - mid-file metadata alone doesn't cause Corrupted anymore (frame delta is only 5)
        report.Status.ShouldBe(AudioIntegrityStatus.Suspect);
        report.MidFileId3v1.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_TruncatedFile_ReturnsTruncated()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateTruncatedFile(
            claimedFrames: 20, actualFrames: 10, bitrateKbps: 128);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Truncated);
        report.ActualFrames.ShouldBe(10);
        report.XingFrames.ShouldBe(20);
        report.FrameDelta.ShouldBe(-10);
    }

    [Fact]
    public async Task ValidateAsync_LargeAlbumArtNoGap_ReturnsClean()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateWithLargeId3v2AndNoGap(
            frameCount: 10, id3v2Size: 8192, bitrateKbps: 128);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        report.Id3v2Size.ShouldNotBeNull();
        report.Id3v2Size.Value.ShouldBeGreaterThan(0);
        report.LargeGap.ShouldBeFalse();
        report.BitrateCliff.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_BufferInput_ReturnsCorrectStatus()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateCleanFile(5, bitrateKbps: 128);
        var validator = CreateValidator();

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        report.FilePath.ShouldBe("(buffer)");
    }

    [Fact]
    public async Task ValidateAsync_FilePathInput_ReturnsCorrectStatus()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateCleanFile(5, bitrateKbps: 128);
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["/music/test.mp3"] = new MockFileData(data),
        });
        var validator = CreateValidator(fileSystem: fs);

        // Act
        var report = await validator.ValidateAsync("/music/test.mp3");

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        report.FilePath.ShouldBe("/music/test.mp3");
    }

    #endregion

    #region Strategy Tests

    [Fact]
    public async Task ValidateAsync_HeuristicOnly_DoesNotSpawnFFmpeg()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateCleanFile(5, bitrateKbps: 128);
        var ffmpeg = Substitute.For<IFFmpegRunner>();
        var validator = CreateValidator(
            config: new AudioIntegrityConfig { Strategy = ValidationStrategy.Heuristic },
            ffmpegRunner: ffmpeg);

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Clean);
        report.Strategy.ShouldBe(ValidationStrategy.Heuristic);
        await ffmpeg.DidNotReceive().RunAsync(Arg.Any<string?>(), Arg.Any<ReadOnlyMemory<byte>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_HybridWithSuspect_RunsFFmpeg()
    {
        // Arrange - small frame delta triggers Suspect, then Hybrid should run FFmpeg
        var data = SyntheticMp3Generator.CreateCleanFile(60, bitrateKbps: 128);
        // Manually create a file with a small Xing mismatch (fewer than FrameTolerance=50)
        // to trigger Suspect. We can't easily do that with the generator, so we use
        // a truncated file with small delta: claimed 55, actual 60 -> delta = +5 (Suspect)
        data = SyntheticMp3Generator.CreateTruncatedFile(claimedFrames: 55, actualFrames: 60, bitrateKbps: 128);

        var ffmpeg = Substitute.For<IFFmpegRunner>();
        ffmpeg.RunAsync(Arg.Any<string?>(), Arg.Any<ReadOnlyMemory<byte>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<string>()));

        var validator = CreateValidator(
            config: new AudioIntegrityConfig { Strategy = ValidationStrategy.Hybrid },
            ffmpegRunner: ffmpeg);

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        await ffmpeg.Received(1).RunAsync(Arg.Any<string?>(), Arg.Any<ReadOnlyMemory<byte>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        report.Strategy.ShouldBe(ValidationStrategy.Hybrid);
    }

    [Fact]
    public async Task ValidateAsync_FFMpegStrategy_AlwaysRunsFFmpeg()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateCleanFile(5, bitrateKbps: 128);
        var ffmpeg = Substitute.For<IFFmpegRunner>();
        ffmpeg.RunAsync(Arg.Any<string?>(), Arg.Any<ReadOnlyMemory<byte>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<string>()));

        var validator = CreateValidator(
            config: new AudioIntegrityConfig { Strategy = ValidationStrategy.FFmpeg },
            ffmpegRunner: ffmpeg);

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        await ffmpeg.Received(1).RunAsync(Arg.Any<string?>(), Arg.Any<ReadOnlyMemory<byte>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        report.Strategy.ShouldBe(ValidationStrategy.FFmpeg);
    }

    [Fact]
    public async Task ValidateAsync_FFMpegReportsErrors_ReturnsCorrupted()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateCleanFile(5, bitrateKbps: 128);
        var ffmpeg = Substitute.For<IFFmpegRunner>();
        ffmpeg.RunAsync(Arg.Any<string?>(), Arg.Any<ReadOnlyMemory<byte>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new[] { "decode error" }));

        var validator = CreateValidator(
            config: new AudioIntegrityConfig { Strategy = ValidationStrategy.FFmpeg },
            ffmpegRunner: ffmpeg);

        // Act
        var report = await validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3);

        // Assert
        report.Status.ShouldBe(AudioIntegrityStatus.Corrupted);
        report.Strategy.ShouldBe(ValidationStrategy.FFmpeg);
    }

    [Fact]
    public async Task ValidateAsync_FFMpegNotInstalled_ThrowsInvalidOperationException()
    {
        // Arrange
        var data = SyntheticMp3Generator.CreateCleanFile(5, bitrateKbps: 128);
        var ffmpeg = Substitute.For<IFFmpegRunner>();
        ffmpeg.RunAsync(Arg.Any<string?>(), Arg.Any<ReadOnlyMemory<byte>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string[]>(new FFmpegNotFoundException("ffmpeg not found")));

        var validator = CreateValidator(
            config: new AudioIntegrityConfig { Strategy = ValidationStrategy.FFmpeg },
            ffmpegRunner: ffmpeg);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => validator.ValidateAsync(data.AsMemory(), AudioFormat.Mp3));
    }

    #endregion
}
