namespace MyMusic.CLI.Tests.Services.Sync;

using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync;
using MyMusic.Common.Services.Sync;
using MyMusic.Common.Services.Sync.Types;
using NSubstitute;
using Shouldly;
using Xunit;

public class PhasesTests
{
    private readonly ISyncApiClient _apiClient;
    private readonly IFileOps _fileOps;
    private readonly IUserPrompt _userPrompt;
    private readonly System.IO.Abstractions.IFileSystem _fileSystem;
    private readonly ISyncConfig _config;
    private readonly IFileSystemScanner _scanner;
    private readonly ILogger<Phases> _logger;

    public PhasesTests()
    {
        _apiClient = Substitute.For<ISyncApiClient>();
        _fileOps = Substitute.For<IFileOps>();
        _userPrompt = Substitute.For<IUserPrompt>();
        _fileSystem = Substitute.For<System.IO.Abstractions.IFileSystem>();
        _config = Substitute.For<ISyncConfig>();
        _scanner = Substitute.For<IFileSystemScanner>();
        _logger = Substitute.For<ILogger<Phases>>();
    }

    [Fact]
    public async Task UploadPhase_WithSyncDirectionDown_SkipsUpload()
    {
        // Arrange
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Direction = SyncDirection.Down });
        var files = new List<ScannedFile> { CreateScannedFile("test.mp3") };

        // Act
        await phases.UploadPhaseAsync(ctx, files, null);

        // Assert
        await _apiClient.DidNotReceive().UploadFileAsync(Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServerActionsPhase_WithSyncDirectionUp_SkipsServerActions()
    {
        // Arrange
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Direction = SyncDirection.Up });
        ctx.PendingActions = [new PendingActionItem { SongId = 1, Path = "test.mp3", Action = "Download" }];

        // Act
        await phases.ServerActionsPhaseAsync(ctx, null);

        // Assert
        await _apiClient.DidNotReceive().DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SyncOptions_DefaultDirectionIsBoth()
    {
        // Arrange & Act
        var options = new SyncOptions();

        // Assert
        options.Direction.ShouldBe(SyncDirection.Both);
    }

    [Fact]
    public void SyncOptions_CanSetDirection()
    {
        // Arrange & Act
        var options = new SyncOptions { Direction = SyncDirection.Up };

        // Assert
        options.Direction.ShouldBe(SyncDirection.Up);
    }

    private Phases CreatePhases()
    {
        return new Phases(_apiClient, _fileOps, _userPrompt, _fileSystem, _config, _scanner, _logger);
    }

    private SyncContext CreateContext(SyncOptions? options = null)
    {
        return new SyncContext
        {
            DeviceId = 1,
            RepositoryPath = "/music",
            SessionId = 1,
            Options = options ?? new SyncOptions()
        };
    }

    private static ScannedFile CreateScannedFile(string path)
    {
        return new ScannedFile
        {
            RelativePath = path,
            FullPath = $"/music/{path}",
            ModifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}
