using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongFileValidateServiceSpecs
{
    private const string FilePath = "/staging/song.mp3";

    private readonly MockFileSystem _fileSystem = new();

    private SongFileValidateService CreateService() =>
        new(_fileSystem, Substitute.For<ILogger<SongFileValidateService>>());

    [Fact]
    public async Task ValidateAsync_ReadableAudioFile_ReturnsNull()
    {
        _fileSystem.Directory.CreateDirectory("/staging");
        _fileSystem.File.WriteAllBytes(FilePath, MockMusicFile.GetTestMusicFile());

        var error = await CreateService().ValidateAsync(FilePath);

        error.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_UnreadableFile_ReturnsErrorMessage()
    {
        _fileSystem.Directory.CreateDirectory("/staging");
        _fileSystem.File.WriteAllBytes(FilePath, [1, 2, 3, 4, 5]);

        var error = await CreateService().ValidateAsync(FilePath);

        error.ShouldNotBeNull();
        error.ShouldStartWith("Cannot read song metadata");
    }

    [Fact]
    public async Task ValidateAsync_MissingFile_ReturnsErrorMessage()
    {
        var error = await CreateService().ValidateAsync(FilePath);

        error.ShouldNotBeNull();
    }
}
