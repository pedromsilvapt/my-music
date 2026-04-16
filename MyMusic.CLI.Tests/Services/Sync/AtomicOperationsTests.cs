namespace MyMusic.CLI.Tests.Services.Sync;

using System.Text;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync;
using MyMusic.Common.Services.Sync;
using MyMusic.Common.Services.Sync.Types;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Shouldly;
using Xunit;

public class AtomicOperationsTests
{
    private readonly System.IO.Abstractions.IFileSystem _fileSystem;
    private readonly ISyncApiClient _apiClient;
    private readonly IFileOps _fileOps;
    private readonly IUserPrompt _userPrompt;
    private readonly ILogger<AtomicOperations> _logger;

    public AtomicOperationsTests()
    {
        _fileSystem = Substitute.For<System.IO.Abstractions.IFileSystem>();
        _apiClient = Substitute.For<ISyncApiClient>();
        _fileOps = Substitute.For<IFileOps>();
        _userPrompt = Substitute.For<IUserPrompt>();
        _logger = Substitute.For<ILogger<AtomicOperations>>();
    }

    [Fact]
    public async Task UploadOneFileAsync_Success_ReturnsCreatedRecord()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);
        var fileInfo = new SyncFileInfo
        {
            Path = "test.mp3",
            ModifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Reason = "New file"
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        mockFile.OpenRead(Arg.Any<string>()).Returns(callInfo =>
        {
            var stream = new MemoryStream();
            return Substitute.For<System.IO.Abstractions.FileSystemStream>(stream, "test.mp3", false);
        });
        _fileSystem.File.Returns(mockFile);

        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UploadFileResult { Success = true, SongId = 1 }));

        // Act
        var result = await atomicOps.UploadOneFileAsync(1, "/music", fileInfo, dryRun: false);

        // Assert
        result.Action.ShouldBe("Created");
        result.Source.ShouldBe("Device");
        result.Reason.ShouldBe("New file");
    }

    [Fact]
    public async Task UploadOneFileAsync_DryRun_ReturnsCreatedRecordWithoutUpload()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);
        var fileInfo = new SyncFileInfo
        {
            Path = "test.mp3",
            ModifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Reason = "New file"
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);

        // Act
        var result = await atomicOps.UploadOneFileAsync(1, "/music", fileInfo, dryRun: true);

        // Assert
        result.Action.ShouldBe("Created");
        await _apiClient.DidNotReceive().UploadFileAsync(Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadOneFileAsync_FileNotFound_ReturnsErrorRecord()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);
        var fileInfo = new SyncFileInfo
        {
            Path = "missing.mp3",
            ModifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(false);
        _fileSystem.File.Returns(mockFile);

        // Act
        var result = await atomicOps.UploadOneFileAsync(1, "/music", fileInfo, dryRun: false);

        // Assert
        result.Action.ShouldBe("Error");
        result.Reason.ShouldBe("File not found");
    }

    [Fact]
    public async Task DownloadOneFileAsync_CreatesParentDirectory()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);

        _fileOps.FileExists(Arg.Any<string>()).Returns(false);

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        _apiClient.DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(stream);

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(false);
        _fileSystem.File.Returns(mockFile);

        // Act
        var result = await atomicOps.DownloadOneFileAsync(1, "/music/sub/test.mp3", "sub/test.mp3", dryRun: false, autoConfirm: true);

        // Assert
        await _fileOps.Received(1).EnsureDirectoryAsync("/music/sub/test.mp3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadOneFileAsync_WithUserConfirmation_OverwritesExisting()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _userPrompt.ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        _apiClient.DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(stream);

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(false);
        _fileSystem.File.Returns(mockFile);

        // Act
        var result = await atomicOps.DownloadOneFileAsync(1, "/music/test.mp3", "test.mp3", dryRun: false, autoConfirm: false);

        // Assert
        result.ShouldNotBeNull();
        result.Action.ShouldBe("Downloaded");
        await _fileOps.Received(1).DeleteFileAsync("/music/test.mp3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadOneFileAsync_UserCancels_ReturnsNull()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _userPrompt.ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await atomicOps.DownloadOneFileAsync(1, "/music/test.mp3", "test.mp3", dryRun: false, autoConfirm: false);

        // Assert
        result.ShouldBeNull();
        await _apiClient.DidNotReceive().DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveOneFileAsync_WithUserConfirmation_DeletesFile()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _userPrompt.ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await atomicOps.RemoveOneFileAsync(1, "/music/test.mp3", "test.mp3", dryRun: false, autoConfirm: false);

        // Assert
        result.ShouldNotBeNull();
        result.Action.ShouldBe("Removed");
        await _fileOps.Received(1).DeleteFileAsync("/music/test.mp3", Arg.Any<CancellationToken>());
        await _apiClient.Received(1).AcknowledgeActionAsync(1, Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveOneFileAsync_UserCancels_ReturnsNull()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _userPrompt.ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await atomicOps.RemoveOneFileAsync(1, "/music/test.mp3", "test.mp3", dryRun: false, autoConfirm: false);

        // Assert
        result.ShouldBeNull();
        await _fileOps.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveOneFileAsync_MissingFile_AcknowledgesWithoutDeleting()
    {
        // Arrange
        var atomicOps = new AtomicOperations(_fileSystem, _apiClient, _fileOps, _userPrompt, _logger);

        _fileOps.FileExists(Arg.Any<string>()).Returns(false);

        // Act
        var result = await atomicOps.RemoveOneFileAsync(1, "/music/test.mp3", "test.mp3", dryRun: false, autoConfirm: false);

        // Assert
        result.ShouldBeNull();
        await _apiClient.Received(1).AcknowledgeActionAsync(1, Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>());
    }
}
