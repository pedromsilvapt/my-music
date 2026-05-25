namespace MyMusic.CLI.Tests.Services.Sync;

using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;
using NSubstitute;
using Shouldly;
using Xunit;

public class OrchestratorTests
{
    private readonly ISyncApiClient _apiClient;
    private readonly ISyncConfig _config;
    private readonly IFileSystemScanner _scanner;
    private readonly IFileOps _fileOps;
    private readonly IKeepAwake _keepAwake;
    private readonly IUserPrompt _userPrompt;
    private readonly ILogger<Phases> _phasesLogger;
    private readonly ILogger<Orchestrator> _orchestratorLogger;
    private readonly System.IO.Abstractions.IFileSystem _fileSystem;
    private readonly SyncActionsDevice _syncActions;

    public OrchestratorTests()
    {
        _apiClient = Substitute.For<ISyncApiClient>();
        _config = Substitute.For<ISyncConfig>();
        _scanner = Substitute.For<IFileSystemScanner>();
        _fileOps = Substitute.For<IFileOps>();
        _keepAwake = Substitute.For<IKeepAwake>();
        _userPrompt = Substitute.For<IUserPrompt>();
        _phasesLogger = Substitute.For<ILogger<Phases>>();
        _orchestratorLogger = Substitute.For<ILogger<Orchestrator>>();
        _fileSystem = Substitute.For<System.IO.Abstractions.IFileSystem>();

        _syncActions = new SyncActionsDevice(_fileOps, _apiClient, _userPrompt, _fileSystem, Substitute.For<ILogger<SyncActionsDevice>>());

        SetupDefaults();
    }

    private void SetupDefaults()
    {
        _config.GetDeviceIdAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<long?>(1L));
        _config.GetRepositoryPath().Returns("/music");
        _config.GetMusicExtensions().Returns([".mp3"]);
        _config.GetExcludePatterns().Returns(Array.Empty<string>());
        _config.GetChunkSize().Returns(10);

        _fileOps.DirectoryExists("/music").Returns(true);
        _scanner.ScanAsync(
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<string[]>(),
                Arg.Any<Action<int, string>?>(),
                Arg.Any<Action<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ScanResult { Files = [], Errors = [] }));

        _apiClient.StartSyncAsync(Arg.Any<long>(), Arg.Any<StartSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StartSyncResult { SessionId = 1 }));
        _apiClient.CreatePendingActionsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CreatePendingActionsResult { Records = [] }));
        _apiClient.CompleteSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CompleteSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CompleteSyncResult()));
        _apiClient.CommitSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommitSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommitSyncResult()));
    }

    private Phases CreatePhases()
    {
        return new Phases(_apiClient, _syncActions, _config, _scanner, _phasesLogger);
    }

    private Orchestrator CreateOrchestrator(Phases phases)
    {
        return new Orchestrator(_config, _fileOps, _keepAwake, phases, _orchestratorLogger);
    }

    [Fact]
    public async Task FullSync_ExecutesAllPhasesInOrder()
    {
        // Arrange
        var phases = CreatePhases();
        var orchestrator = CreateOrchestrator(phases);

        // Act
        var result = await orchestrator.OrchestrateSyncAsync(new SyncOptions(), null);

        // Assert
        await _keepAwake.Received(1).ActivateAsync(Arg.Any<CancellationToken>());
        await _scanner.Received(1).ScanAsync(
            Arg.Any<string>(),
            Arg.Any<string[]>(),
            Arg.Any<string[]>(),
            Arg.Any<Action<int, string>?>(),
            Arg.Any<Action<string, string>?>(),
            Arg.Any<CancellationToken>());
        await _apiClient.Received(1).StartSyncAsync(1, Arg.Any<StartSyncRequest>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).CreatePendingActionsAsync(1, Arg.Any<long>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).CommitSyncAsync(1, 1, Arg.Any<CommitSyncRequest>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).CompleteSyncAsync(1, 1, Arg.Any<CompleteSyncRequest>(), Arg.Any<CancellationToken>());
        _keepAwake.Received(1).Deactivate();
    }

    [Fact]
    public async Task SyncDirectionUp_SkipsServerActionsPhase()
    {
        // Arrange
        var options = new SyncOptions { Direction = SyncDirection.Up };
        var phases = CreatePhases();
        var orchestrator = CreateOrchestrator(phases);

        // Act
        await orchestrator.OrchestrateSyncAsync(options, null);

        // Assert
        await _apiClient.DidNotReceive().DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncDirectionDown_SkipsUploadPhase()
    {
        // Arrange
        var options = new SyncOptions { Direction = SyncDirection.Down };
        var phases = CreatePhases();
        var orchestrator = CreateOrchestrator(phases);

        // Act
        await orchestrator.OrchestrateSyncAsync(options, null);

        // Assert
        await _apiClient.DidNotReceive().UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncDirectionBoth_ExecutesAllPhases()
    {
        // Arrange
        var options = new SyncOptions { Direction = SyncDirection.Both };
        var phases = CreatePhases();
        var orchestrator = CreateOrchestrator(phases);

        // Act
        await orchestrator.OrchestrateSyncAsync(options, null);

        // Assert
        await _apiClient.Received(1).StartSyncAsync(Arg.Any<long>(), Arg.Any<StartSyncRequest>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).CommitSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommitSyncRequest>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).CompleteSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CompleteSyncRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SyncResult_HasCancelledProperty()
    {
        // Arrange
        var result = new SyncResult { Cancelled = true };

        // Assert
        result.Cancelled.ShouldBeTrue();
    }

    [Fact]
    public void SyncProgress_HasExpectedStructure()
    {
        // Arrange
        var progress = SyncProgress.FromResult(
            new SyncResult
            {
                CreateRemote = 2,
                UpdateRemote = 1,
                Skipped = 1,
                CreateLocal = 1,
                UpdateLocal = 0,
                Delete = 0,
                Link = 0,
                Unlink = 0,
                Rename = 0,
                Conflict = 0,
                UpdateTimestamp = 0,
                Error = 0
            },
            "upload", 10, 5);

        // Assert
        progress.TotalFiles.ShouldBe(10);
        progress.ProcessedFiles.ShouldBe(5);
        progress.Phase.ShouldBe("upload");
        progress.Result.CreateRemote.ShouldBe(2);
        progress.Result.UpdateRemote.ShouldBe(1);
    }
}