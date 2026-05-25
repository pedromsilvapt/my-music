namespace MyMusic.CLI.Tests.Services.Sync;

using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;
using NSubstitute;
using Shouldly;
using Xunit;

public class PhasesTests
{
    private readonly ISyncApiClient _apiClient;
    private readonly SyncActionsDevice _syncActions;
    private readonly ISyncConfig _config;
    private readonly IFileSystemScanner _scanner;
    private readonly ILogger<Phases> _logger;
    private readonly IFileOps _fileOps;
    private readonly IUserPrompt _userPrompt;
    private readonly System.IO.Abstractions.IFileSystem _fileSystem;

    public PhasesTests()
    {
        _apiClient = Substitute.For<ISyncApiClient>();
        _fileOps = Substitute.For<IFileOps>();
        _userPrompt = Substitute.For<IUserPrompt>();
        _fileSystem = Substitute.For<System.IO.Abstractions.IFileSystem>();
        _config = Substitute.For<ISyncConfig>();
        _scanner = Substitute.For<IFileSystemScanner>();
        _logger = Substitute.For<ILogger<Phases>>();

        _syncActions = new SyncActionsDevice(_fileOps, _apiClient, _userPrompt, _fileSystem, Substitute.For<ILogger<SyncActionsDevice>>());
    }

    [Fact]
    public async Task UploadPhase_WithSyncDirectionDown_SkipsUpload()
    {
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Direction = SyncDirection.Down });
        var files = new List<ScannedFile> { CreateScannedFile("test.mp3") };

        await phases.UploadPhaseAsync(ctx, files, null);

        await _apiClient.DidNotReceive().UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServerActionsPhase_WithSyncDirectionUp_SkipsServerActions()
    {
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Direction = SyncDirection.Up });

        await phases.ServerActionsPhaseAsync(ctx, null);

        await _apiClient.DidNotReceive().DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitPhase_CallsCommitEndpointWithDirection()
    {
        var phases = CreatePhases();
        var ctx = CreateContext();

        _apiClient.CommitSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommitSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommitSyncResult()));

        await phases.CommitPhaseAsync(ctx, null);

        await _apiClient.Received(1).CommitSyncAsync(1, 1, Arg.Is<CommitSyncRequest>(r => r.Direction == "both"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitPhase_WithDirectionUp_SendsUpDirection()
    {
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Direction = SyncDirection.Up });

        _apiClient.CommitSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommitSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommitSyncResult()));

        await phases.CommitPhaseAsync(ctx, null);

        await _apiClient.Received(1).CommitSyncAsync(1, 1, Arg.Is<CommitSyncRequest>(r => r.Direction == "up"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitPhase_WithDirectionDown_SendsDownDirection()
    {
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Direction = SyncDirection.Down });

        _apiClient.CommitSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommitSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommitSyncResult()));

        await phases.CommitPhaseAsync(ctx, null);

        await _apiClient.Received(1).CommitSyncAsync(1, 1, Arg.Is<CommitSyncRequest>(r => r.Direction == "down"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SyncOptions_DefaultDirectionIsBoth()
    {
        var options = new SyncOptions();
        options.Direction.ShouldBe(SyncDirection.Both);
    }

    [Fact]
    public void SyncOptions_CanSetDirection()
    {
        var options = new SyncOptions { Direction = SyncDirection.Up };
        options.Direction.ShouldBe(SyncDirection.Up);
    }

    private Phases CreatePhases()
    {
        return new Phases(_apiClient, _syncActions, _config, _scanner, _logger);
    }

    private static SyncContext CreateContext(SyncOptions? options = null)
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