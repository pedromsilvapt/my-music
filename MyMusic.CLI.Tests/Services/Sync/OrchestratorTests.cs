namespace MyMusic.CLI.Tests.Services.Sync;

using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync;
using MyMusic.Common.Services.Sync;
using MyMusic.Common.Services.Sync.Types;
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

        SetupDefaults();
    }

    private void SetupDefaults()
    {
        _config.GetDeviceIdAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<long?>(1L));
        _config.GetRepositoryPath().Returns("/music");
        _config.GetMusicExtensions().Returns([".mp3"]);
        _config.GetExcludePatterns().Returns(Array.Empty<string>());
        _config.GetChunkSize().Returns(10);

        _fileOps.FileExists("/music").Returns(true);
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
        _apiClient.GetPendingActionsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetPendingActionsResult { Actions = [] }));
        _apiClient.CompleteSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CompleteSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CompleteSyncResult()));
    }

    private Phases CreatePhases()
    {
        return new Phases(_apiClient, _fileOps, _userPrompt, _fileSystem, _config, _scanner, _phasesLogger);
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
        await _apiClient.Received(1).GetPendingActionsAsync(1, Arg.Any<CancellationToken>());
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

        // Assert - Server actions should not process downloads
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

        // Assert - Upload should not be called
        await _apiClient.DidNotReceive().UploadFileAsync(Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>());
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
        await _apiClient.Received(1).CompleteSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CompleteSyncRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SyncResult_HasCancelledProperty()
    {
        // Arrange & Act
        var result = new SyncResult { Cancelled = true };

        // Assert
        result.Cancelled.ShouldBeTrue();
    }

    [Fact]
    public void SyncProgress_HasExpectedStructure()
    {
        // Arrange & Act
        var progress = SyncProgress.FromResult(
            new SyncResult
            {
                Created = 2,
                Updated = 1,
                Skipped = 1,
                Downloaded = 1,
                Removed = 0,
                Failed = 0,
                Conflicts = 0
            },
            "upload", 10, 5);

        // Assert
        progress.TotalFiles.ShouldBe(10);
        progress.ProcessedFiles.ShouldBe(5);
        progress.Phase.ShouldBe("upload");
        progress.Result.Created.ShouldBe(2);
        progress.Result.Updated.ShouldBe(1);
    }
}
