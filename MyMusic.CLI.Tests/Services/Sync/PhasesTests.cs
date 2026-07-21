namespace MyMusic.CLI.Tests.Services.Sync;

using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;
using NSubstitute;
using Shouldly;
using Xunit;

internal sealed class CapturingProgress : IProgress<SyncProgress>
{
    public List<SyncProgress> Reports { get; } = new();

    void IProgress<SyncProgress>.Report(SyncProgress value) => Reports.Add(value);
}

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

    [Fact]
    public async Task UploadPhase_ReportsProgressPerChunkDuringCheck_AllSkipped()
    {
        // Setup: 5 files, chunk size 2 -> 3 chunks (2,2,1). All files skipped by server.
        _config.GetChunkSize().Returns(2);
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult
            {
                Records = new List<SyncRecordItem>(),
                Counts = new SyncActionCounts { SkippedCount = 2 }
            });

        var phases = CreatePhases();
        var ctx = CreateContext();
        var files = Enumerable.Range(1, 5).Select(i => CreateScannedFile($"song{i}.mp3")).ToList();

        var progress = new CapturingProgress();

        await phases.UploadPhaseAsync(ctx, files, progress);

        // Each chunk produces exactly one end-of-chunk report with the accumulated processedCount.
        // Chunk 1 -> processedCount=2, Chunk 2 -> processedCount=4, Chunk 3 -> processedCount=5
        var chunkReports = progress.Reports.Where(r => r.Phase == "upload").ToList();
        chunkReports.Count.ShouldBe(3);
        chunkReports[0].ProcessedFiles.ShouldBe(2);
        chunkReports[1].ProcessedFiles.ShouldBe(4);
        chunkReports[2].ProcessedFiles.ShouldBe(5);
        chunkReports.All(r => r.TotalFiles == 5).ShouldBeTrue();
    }

    [Fact]
    public async Task UploadPhase_ReportsPerFileProgressThenEndOfChunkTopsUp()
    {
        // Setup: 3 files, chunk size 3 -> 1 chunk. Server requests CreateRemote for 2 of them.
        _config.GetChunkSize().Returns(3);
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult
            {
                Records = new List<SyncRecordItem>
                {
                    CreateRecord("song1.mp3", SyncRecordAction.CreateRemote),
                    CreateRecord("song2.mp3", SyncRecordAction.CreateRemote),
                    CreateRecord("song3.mp3", SyncRecordAction.Skipped)
                },
                Counts = new SyncActionCounts { SkippedCount = 1, CreateRemoteCount = 2 }
            });

        // Make ActionCreateRemote succeed without touching the real filesystem.
        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        mockFile.OpenRead(Arg.Any<string>()).Returns(callInfo =>
        {
            var stream = new MemoryStream();
            return Substitute.For<System.IO.Abstractions.FileSystemStream>(stream, "test.mp3", false);
        });
        _fileSystem.File.Returns(mockFile);
        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UploadFileResult { Success = true, SongId = 1, Counts = new SyncActionCounts { CreateRemoteCount = 1 } });

        var phases = CreatePhases();
        var ctx = CreateContext();
        var files = Enumerable.Range(1, 3).Select(i => CreateScannedFile($"song{i}.mp3")).ToList();

        var progress = new CapturingProgress();

        await phases.UploadPhaseAsync(ctx, files, progress);

        var chunkReports = progress.Reports.Where(r => r.Phase == "upload").ToList();
        // Per-file reports for song1 (1) and song2 (2), then end-of-chunk top-up to 3.
        chunkReports.Count.ShouldBe(3);
        chunkReports[0].ProcessedFiles.ShouldBe(1);
        chunkReports[0].CurrentFile.ShouldBe("song1.mp3");
        chunkReports[1].ProcessedFiles.ShouldBe(2);
        chunkReports[1].CurrentFile.ShouldBe("song2.mp3");
        chunkReports[2].ProcessedFiles.ShouldBe(3);
        chunkReports[2].CurrentFile.ShouldBe("");
        chunkReports.All(r => r.TotalFiles == 3).ShouldBeTrue();
    }

    [Fact]
    public async Task UploadPhase_ReportedProgressNeverDecreasesWithinChunk()
    {
        // Setup: 4 files, chunk size 2 -> 2 chunks. First chunk: 1 CreateRemote, 1 Skipped.
        // Second chunk: 2 UpdateRemote.
        _config.GetChunkSize().Returns(2);
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new CheckSyncResult
                {
                    Records = new List<SyncRecordItem>
                    {
                        CreateRecord("song1.mp3", SyncRecordAction.CreateRemote),
                        CreateRecord("song2.mp3", SyncRecordAction.Skipped)
                    },
                    Counts = new SyncActionCounts { SkippedCount = 1, CreateRemoteCount = 1 }
                },
                new CheckSyncResult
                {
                    Records = new List<SyncRecordItem>
                    {
                        CreateRecord("song3.mp3", SyncRecordAction.UpdateRemote),
                        CreateRecord("song4.mp3", SyncRecordAction.UpdateRemote)
                    },
                    Counts = new SyncActionCounts { UpdateRemoteCount = 2 }
                });

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        mockFile.OpenRead(Arg.Any<string>()).Returns(callInfo =>
        {
            var stream = new MemoryStream();
            return Substitute.For<System.IO.Abstractions.FileSystemStream>(stream, "test.mp3", false);
        });
        _fileSystem.File.Returns(mockFile);
        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UploadFileResult { Success = true, SongId = 1, Counts = new SyncActionCounts { CreateRemoteCount = 1 } });

        var phases = CreatePhases();
        var ctx = CreateContext();
        var files = Enumerable.Range(1, 4).Select(i => CreateScannedFile($"song{i}.mp3")).ToList();

        var progress = new CapturingProgress();

        await phases.UploadPhaseAsync(ctx, files, progress);

        var chunkReports = progress.Reports.Where(r => r.Phase == "upload").ToList();
        // Chunk 1: per-file (1), end-of-chunk (2).
        // Chunk 2: per-file (3), per-file (4), end-of-chunk (4).
        var processedValues = chunkReports.Select(r => r.ProcessedFiles).ToList();
        processedValues.ShouldBe(new[] { 1, 2, 3, 4, 4 });
        // Monotonically non-decreasing.
        for (var i = 1; i < processedValues.Count; i++)
        {
            processedValues[i].ShouldBeGreaterThanOrEqualTo(processedValues[i - 1]);
        }
        // Never exceeds total.
        processedValues.Max().ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task UploadPhase_CheckFailureReportsChunkProgressAndContinues()
    {
        // Setup: 3 files, chunk size 2 -> 2 chunks. First chunk fails, second succeeds (all skipped).
        _config.GetChunkSize().Returns(2);
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new Exception("network error"),
                _ => Task.FromResult(new CheckSyncResult
                {
                    Records = new List<SyncRecordItem>(),
                    Counts = new SyncActionCounts { SkippedCount = 1 }
                }));

        var phases = CreatePhases();
        var ctx = CreateContext();
        var files = Enumerable.Range(1, 3).Select(i => CreateScannedFile($"song{i}.mp3")).ToList();

        var progress = new CapturingProgress();

        await phases.UploadPhaseAsync(ctx, files, progress);

        var chunkReports = progress.Reports.Where(r => r.Phase == "upload").ToList();
        // Chunk 1 failed -> processedCount=2 with error message.
        // Chunk 2 succeeded -> processedCount=3 (end-of-chunk report).
        chunkReports.Count.ShouldBe(2);
        chunkReports[0].ProcessedFiles.ShouldBe(2);
        chunkReports[0].ErrorMessage.ShouldBeOfType<string>();
        chunkReports[0].ErrorMessage!.ShouldContain("Chunk 1 failed");
        chunkReports[1].ProcessedFiles.ShouldBe(3);
        chunkReports[1].ErrorMessage.ShouldBeNull();

        ctx.Result.Error.ShouldBe(2);
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

    private static SyncRecordItem CreateRecord(string path, SyncRecordAction action)
    {
        return new SyncRecordItem
        {
            Id = Random.Shared.NextInt64(),
            FilePath = path,
            Action = action,
            Data = null
        };
    }
}