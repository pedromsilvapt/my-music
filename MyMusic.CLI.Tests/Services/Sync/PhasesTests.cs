namespace MyMusic.CLI.Tests.Services.Sync;

using System.Text.Json;
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
        _userPrompt.PromptConflictResolutionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConflictResolution>>(), Arg.Any<CancellationToken>())
            .Returns(ConflictResolution.Skip);
        _fileSystem = Substitute.For<System.IO.Abstractions.IFileSystem>();
        _config = Substitute.For<ISyncConfig>();
        _scanner = Substitute.For<IFileSystemScanner>();
        _logger = Substitute.For<ILogger<Phases>>();

        _syncActions = new SyncActionsDevice(_fileOps, _apiClient, _userPrompt, _fileSystem, _config, Substitute.For<ILogger<SyncActionsDevice>>());
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

    [Theory]
    [InlineData(SyncDirection.Down, true)]
    [InlineData(SyncDirection.Down, false)]
    [InlineData(SyncDirection.Both, true)]
    [InlineData(SyncDirection.Both, false)]
    public async Task ServerActionsPhase_AcknowledgesDeleteLocalAndRenameRecords(SyncDirection direction, bool dryRun)
    {
        // The server marked one song for removal and renamed another; the local files exist
        var deleteRecord = CreateRecord("removed.mp3", SyncRecordAction.DeleteLocal);
        var renameRecord = CreateRecord("new/renamed.mp3", SyncRecordAction.Rename) with
        {
            Data = JsonSerializer.SerializeToElement(new { previousPath = "old/renamed.mp3", newPath = "new/renamed.mp3" })
        };
        _apiClient.CreatePendingActionsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePendingActionsResult { Records = [deleteRecord, renameRecord] });
        _apiClient.AcknowledgeActionAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcknowledgeActionResult { Success = true });
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Direction = direction, DryRun = dryRun, AutoConfirm = true });

        await phases.ServerActionsPhaseAsync(ctx, null);

        // Both records should be acknowledged, otherwise the commit rejects the session
        await _apiClient.Received(1).AcknowledgeActionAsync(1, 1,
            Arg.Is<AcknowledgeActionRequest>(r => r.RecordIds.Contains(deleteRecord.Id)), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).AcknowledgeActionAsync(1, 1,
            Arg.Is<AcknowledgeActionRequest>(r => r.RecordIds.Contains(renameRecord.Id)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServerActionsPhase_CountsRecordsWhenCreated_NotWhenAcknowledged()
    {
        // The server marks a song for removal, and counts the record in the response that creates it
        var deleteRecord = CreateRecord("removed.mp3", SyncRecordAction.DeleteLocal);
        _apiClient.CreatePendingActionsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePendingActionsResult { Records = [deleteRecord], Counts = new SyncActionCounts { DeleteLocalCount = 1 } });
        _apiClient.AcknowledgeActionAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcknowledgeActionResult { Success = true });
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { AutoConfirm = true });

        await phases.ServerActionsPhaseAsync(ctx, null);

        // The removal should be acknowledged, and counted once
        await _apiClient.Received(1).AcknowledgeActionAsync(1, 1,
            Arg.Is<AcknowledgeActionRequest>(r => r.RecordIds.Contains(deleteRecord.Id)), Arg.Any<CancellationToken>());
        ctx.Result.DeleteLocal.ShouldBe(1);
    }

    [Theory]
    [InlineData(SyncDirection.Both)]
    [InlineData(SyncDirection.Up)]
    [InlineData(SyncDirection.Down)]
    public async Task StartSession_SendsDirection(SyncDirection direction)
    {
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Direction = direction });

        _apiClient.StartSyncAsync(Arg.Any<long>(), Arg.Any<StartSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StartSyncResult { SessionId = 1 }));

        await phases.StartSessionAsync(ctx, [], default);

        await _apiClient.Received(1).StartSyncAsync(1, Arg.Is<StartSyncRequest>(r => r.Direction == direction), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartSession_DryRun_SendsLocalNamingTemplateAsDeviceOptions()
    {
        // A dry run doesn't save the device options, so the session should preview the local template
        _config.GetNamingTemplate().Returns("{{ year }}/{{ simple_label }}.mp3");
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { DryRun = true });

        _apiClient.StartSyncAsync(Arg.Any<long>(), Arg.Any<StartSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StartSyncResult { SessionId = 1 }));

        await phases.StartSessionAsync(ctx, [], default);

        await _apiClient.Received(1).StartSyncAsync(1,
            Arg.Is<StartSyncRequest>(r => r.DeviceOptions != null && r.DeviceOptions.NamingTemplate == "{{ year }}/{{ simple_label }}.mp3"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartSession_RealRun_DoesNotSendDeviceOptions()
    {
        // A real run saves the device options beforehand; the server rejects an override
        _config.GetNamingTemplate().Returns("{{ year }}/{{ simple_label }}.mp3");
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { DryRun = false });

        _apiClient.StartSyncAsync(Arg.Any<long>(), Arg.Any<StartSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StartSyncResult { SessionId = 1 }));

        await phases.StartSessionAsync(ctx, [], default);

        await _apiClient.Received(1).StartSyncAsync(1,
            Arg.Is<StartSyncRequest>(r => r.DeviceOptions == null), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, SyncDirection.Both)]
    [InlineData(true, SyncDirection.Down)]
    public async Task PrepareDeduplicatePhase_WithoutUploadDeduplication_IsSkipped(bool deduplicate, SyncDirection direction)
    {
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Deduplicate = deduplicate, Direction = direction });

        await phases.PrepareDeduplicatePhaseAsync(ctx, null);

        await _apiClient.DidNotReceive().PrepareDeduplicateAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SyncDirection.Both)]
    [InlineData(SyncDirection.Up)]
    public async Task PrepareDeduplicatePhase_PreparesUntilDone_ReportingProgress(SyncDirection direction)
    {
        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Deduplicate = true, Direction = direction });
        var progress = new CapturingProgress();

        _apiClient.PrepareDeduplicateAsync(1, 1, Arg.Any<CancellationToken>())
            .Returns(
                new PrepareDeduplicateResult { Total = 45, Processed = 20 },
                new PrepareDeduplicateResult { Total = 45, Processed = 40 },
                new PrepareDeduplicateResult { Total = 45, Processed = 45, Done = true });

        await phases.PrepareDeduplicatePhaseAsync(ctx, progress);

        await _apiClient.Received(3).PrepareDeduplicateAsync(1, 1, Arg.Any<CancellationToken>());
        progress.Reports.ShouldAllBe(p => p.Phase == "fingerprinting");
        progress.Reports.Where(p => p.TotalFiles > 0).Select(p => p.ProcessedFiles).ShouldBe([20, 40, 45]);
        progress.Reports.ShouldAllBe(p => p.TotalFiles == 0 || p.TotalFiles == 45);
    }

    [Fact]
    public async Task CommitPhase_CallsCommitEndpoint()
    {
        var phases = CreatePhases();
        var ctx = CreateContext();

        _apiClient.CommitSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommitSyncResult()));

        await phases.CommitPhaseAsync(ctx, null);

        await _apiClient.Received(1).CommitSyncAsync(1, 1, Arg.Any<CancellationToken>());
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
        var chunkReports = ChunkReports(progress, files.Count);
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

        var chunkReports = ChunkReports(progress, files.Count);
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
    public async Task UploadPhase_QueuesUpdateLocalReturnedByUpload()
    {
        // The server asks for the changed local file, which turns out to be a previous version of its song:
        // the upload answers with an UpdateLocal, so the device downloads the current version this session
        _config.GetChunkSize().Returns(10);
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult
            {
                Records = [CreateRecord("song.mp3", SyncRecordAction.UpdateRemote)],
                Counts = new SyncActionCounts { UpdateRemoteCount = 1 }
            });
        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        mockFile.OpenRead(Arg.Any<string>()).Returns(_ =>
            Substitute.For<System.IO.Abstractions.FileSystemStream>(new MemoryStream(), "song.mp3", false));
        _fileSystem.File.Returns(mockFile);
        var updateLocalRecord = CreateRecord("song.mp3", SyncRecordAction.UpdateLocal);
        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UploadFileResult
            {
                Success = true,
                Records = [updateLocalRecord],
                Counts = new SyncActionCounts { UpdateLocalCount = 1 }
            });

        var phases = CreatePhases();
        var ctx = CreateContext();

        await phases.UploadPhaseAsync(ctx, [CreateScannedFile("song.mp3")], null);

        ctx.PendingServerRecords.ShouldContain(updateLocalRecord);
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

        var chunkReports = ChunkReports(progress, files.Count);
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

        var chunkReports = ChunkReports(progress, files.Count);
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

    [Fact]
    public async Task UploadPhase_ConflictFromEarlierChunk_StaysMarkedAfterLaterChunkIsResolved()
    {
        // Chunk 1 holds a real conflict; chunk 2 holds a file the server changed, which resolves to an UpdateLocal
        _config.GetChunkSize().Returns(1);
        SetupLocalFilesExist();
        var conflict = CreateRecord("conflict.mp3", SyncRecordAction.Conflict) with { SongId = 1 };
        var potentialUpdate = CreateRecord("changed.mp3", SyncRecordAction.UpdateLocal) with { SongId = 2 };
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult { Records = [conflict], Counts = SyncActionCounts.Empty }, new CheckSyncResult { Records = [potentialUpdate], Counts = SyncActionCounts.Empty });
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ResolveConflictsResult { Records = [CreateRecord("conflict.mp3", SyncRecordAction.Conflict) with { SongId = 1 }] },
                new ResolveConflictsResult { Records = [CreateRecord("changed.mp3", SyncRecordAction.UpdateLocal) with { SongId = 2 }] });

        var phases = CreatePhases();
        var ctx = CreateContext();

        await phases.UploadPhaseAsync(ctx, [CreateScannedFile("conflict.mp3"), CreateScannedFile("changed.mp3")], null);

        // The conflict found in chunk 1 should still protect its file; the resolved update should not
        ctx.ConflictedPaths.ShouldBe(["conflict.mp3"]);
    }

    [Fact]
    public async Task UploadPhase_ConflictResolvedToUpdateLocal_IsNotMarkedAndIsQueued()
    {
        // The local file is a previous version of the song, so the server version wins
        _config.GetChunkSize().Returns(10);
        SetupLocalFilesExist();
        var conflict = CreateRecord("song.mp3", SyncRecordAction.Conflict) with { SongId = 1 };
        var updateLocal = CreateRecord("song.mp3", SyncRecordAction.UpdateLocal) with { SongId = 1 };
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult { Records = [conflict], Counts = SyncActionCounts.Empty });
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [updateLocal] });

        var phases = CreatePhases();
        var ctx = CreateContext();

        await phases.UploadPhaseAsync(ctx, [CreateScannedFile("song.mp3")], null);

        // The song should be downloaded in the server actions phase
        ctx.ConflictedPaths.ShouldBeEmpty();
        ctx.PendingServerRecords.ShouldContain(updateLocal);
        ctx.PendingServerRecords.ShouldNotContain(conflict);
    }

    [Fact]
    public async Task UploadPhase_UserChoosesDownload_ConflictIsNotMarkedAndDownloadIsQueued()
    {
        _config.GetChunkSize().Returns(10);
        SetupLocalFilesExist();
        var conflict = CreateRecord("song.mp3", SyncRecordAction.Conflict) with { SongId = 1 };
        var resolvedConflict = CreateRecord("song.mp3", SyncRecordAction.Conflict) with { Id = 10, SongId = 1 };
        var updateLocal = CreateRecord("song.mp3", SyncRecordAction.UpdateLocal) with { Id = 11, SongId = 1, ResolvesConflictRecordId = 10 };
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult { Records = [conflict], Counts = SyncActionCounts.Empty });
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [resolvedConflict], Counts = new SyncActionCounts { ConflictCount = 1 } });
        _apiClient.ChooseConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [updateLocal], Counts = new SyncActionCounts { UpdateLocalCount = 1, ConflictCount = -1 } });

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Conflicts = ConflictResolution.Download });

        await phases.UploadPhaseAsync(ctx, [CreateScannedFile("song.mp3")], null);

        // The server's version should be downloaded in the server actions phase
        ctx.ConflictedPaths.ShouldBeEmpty();
        ctx.PendingServerRecords.ShouldBe([updateLocal]);
        ctx.Result.Conflict.ShouldBe(0);
        ctx.Result.UpdateLocal.ShouldBe(1);
    }

    [Fact]
    public async Task UploadPhase_UserChoosesUpload_ConflictIsNotMarkedAndPathIsUploaded()
    {
        _config.GetChunkSize().Returns(10);
        SetupLocalFilesExist();
        var conflict = CreateRecord("song.mp3", SyncRecordAction.Conflict) with { SongId = 1 };
        var resolvedConflict = CreateRecord("song.mp3", SyncRecordAction.Conflict) with { Id = 10, SongId = 1 };
        var updateRemote = CreateRecord("song.mp3", SyncRecordAction.UpdateRemote) with { Id = 11, SongId = 1, ResolvesConflictRecordId = 10 };
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult { Records = [conflict], Counts = SyncActionCounts.Empty });
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [resolvedConflict], Counts = new SyncActionCounts { ConflictCount = 1 } });
        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UploadFileResult { Success = true, SongId = 1, Records = [updateRemote], Counts = new SyncActionCounts { UpdateRemoteCount = 1, ConflictCount = -1 } });

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { Conflicts = ConflictResolution.Upload });

        await phases.UploadPhaseAsync(ctx, [CreateScannedFile("song.mp3")], null);

        ctx.ConflictedPaths.ShouldBeEmpty();
        ctx.UploadedPaths.ShouldBe(["song.mp3"]);
        ctx.Result.Conflict.ShouldBe(0);
        ctx.Result.UpdateRemote.ShouldBe(1);
    }

    [Theory]
    [InlineData(SyncRecordAction.UpdateTimestamp)]
    [InlineData(SyncRecordAction.Skipped)]
    public async Task UploadPhase_ConflictSettledWithoutDownload_IsNotMarked(SyncRecordAction resolvedAction)
    {
        // The contents turn out to be equal (or the server skips the file), so there is nothing to protect
        _config.GetChunkSize().Returns(10);
        SetupLocalFilesExist();
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult { Records = [CreateRecord("song.mp3", SyncRecordAction.Conflict) with { SongId = 1 }], Counts = SyncActionCounts.Empty });
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [CreateRecord("song.mp3", resolvedAction) with { SongId = 1 }] });

        var phases = CreatePhases();
        var ctx = CreateContext();

        await phases.UploadPhaseAsync(ctx, [CreateScannedFile("song.mp3")], null);

        ctx.ConflictedPaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadPhase_ResolveRequestFails_ConflictStaysMarked()
    {
        // The server could not compare the contents, so the conflict was never settled
        _config.GetChunkSize().Returns(10);
        SetupLocalFilesExist();
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult { Records = [CreateRecord("song.mp3", SyncRecordAction.Conflict) with { SongId = 1 }], Counts = SyncActionCounts.Empty });
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns<ResolveConflictsResult>(_ => throw new HttpRequestException("server unavailable"));

        var phases = CreatePhases();
        var ctx = CreateContext();

        await phases.UploadPhaseAsync(ctx, [CreateScannedFile("song.mp3")], null);

        // The local file should stay protected from downloads
        ctx.ConflictedPaths.ShouldBe(["song.mp3"]);
    }

    [Fact]
    public async Task ServerActionsPhase_DownloadOverConflictedPath_IsReportedNotPerformed()
    {
        // The server asks to update a file whose local copy has an unresolved conflict
        var download = CreateRecord("song.mp3", SyncRecordAction.UpdateLocal) with { SongId = 1 };
        SetupPendingActions(download);
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { AutoConfirm = true });
        ctx.ConflictedPaths.Add("song.mp3");

        await phases.ServerActionsPhaseAsync(ctx, null);

        // The local file should not be overwritten, and the skipped record should be reported so the commit accepts it
        await _apiClient.DidNotReceive().DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await AssertErrorReported(download, "Unresolved conflict");
    }

    [Fact]
    public async Task ServerActionsPhase_DownloadForOtherPathOfConflictedSong_IsPerformed()
    {
        // The song is linked at two paths and only one of them conflicts
        var otherPath = CreateRecord("copy.mp3", SyncRecordAction.UpdateLocal) with { SongId = 1 };
        SetupPendingActions(otherPath);
        SetupDownloadSucceeds();

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { AutoConfirm = true });
        ctx.ConflictedPaths.Add("song.mp3");

        await phases.ServerActionsPhaseAsync(ctx, null);

        // The path without a conflict should still be updated
        await _apiClient.Received(1).DownloadSongAsync(1, Arg.Any<CancellationToken>());
        await _apiClient.Received(1).AcknowledgeActionAsync(1, 1,
            Arg.Is<AcknowledgeActionRequest>(r => r.RecordIds.Contains(otherPath.Id)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServerActionsPhase_CreateLocalForConflictedSong_IsPerformed()
    {
        // A new path for a song with a conflict elsewhere: a create never writes over an existing file
        var create = CreateRecord("new.mp3", SyncRecordAction.CreateLocal) with { SongId = 1 };
        SetupPendingActions(create);
        SetupDownloadSucceeds();
        _fileOps.FileExists(Arg.Any<string>()).Returns(false);

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { AutoConfirm = true });
        ctx.ConflictedPaths.Add("song.mp3");

        await phases.ServerActionsPhaseAsync(ctx, null);

        await _apiClient.Received(1).DownloadSongAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServerActionsPhase_RenameOfConflictedPath_IsReportedNotPerformed()
    {
        // The server renames a file whose local copy has an unresolved conflict
        var rename = CreateRecord("new/song.mp3", SyncRecordAction.Rename) with
        {
            SongId = 1,
            Data = JsonSerializer.SerializeToElement(new { previousPath = "song.mp3", newPath = "new/song.mp3" })
        };
        SetupPendingActions(rename);
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { AutoConfirm = true });
        ctx.ConflictedPaths.Add("song.mp3");

        await phases.ServerActionsPhaseAsync(ctx, null);

        // The conflicted file should stay where it is
        await _fileOps.DidNotReceive().MoveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await AssertErrorReported(rename, "Unresolved conflict");
    }

    public static TheoryData<string?> MissingRenameData => new() { null, "{}", """{"newPath":"new/song.mp3"}""" };

    [Theory]
    [MemberData(nameof(MissingRenameData))]
    public async Task ServerActionsPhase_RenameWithMissingData_IsReported(string? data)
    {
        // The server sent a Rename without the path to move the file from
        var rename = CreateRecord("new/song.mp3", SyncRecordAction.Rename) with
        {
            Data = data == null ? null : JsonDocument.Parse(data).RootElement.Clone()
        };
        SetupPendingActions(rename);

        var phases = CreatePhases();
        var ctx = CreateContext(options: new SyncOptions { AutoConfirm = true });

        await phases.ServerActionsPhaseAsync(ctx, null);

        // Nothing should be moved, and the record should be reported so the commit accepts it
        await _fileOps.DidNotReceive().MoveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await AssertErrorReported(rename, "Missing rename data");
    }

    [Theory]
    [InlineData(SyncRecordAction.Conflict)]
    [InlineData(SyncRecordAction.Error)]
    public async Task UploadPhase_UnsettledConflict_MarksItsPathOnly(SyncRecordAction resolvedAction)
    {
        // The song is linked at two paths; only one of them is checked as a conflict
        _config.GetChunkSize().Returns(10);
        SetupLocalFilesExist();
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult { Records = [CreateRecord("song.mp3", SyncRecordAction.Conflict) with { SongId = 1 }], Counts = SyncActionCounts.Empty });
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [CreateRecord("song.mp3", resolvedAction) with { SongId = 1 }] });

        var phases = CreatePhases();
        var ctx = CreateContext();

        await phases.UploadPhaseAsync(ctx, [CreateScannedFile("song.mp3"), CreateScannedFile("copy.mp3")], null);

        ctx.ConflictedPaths.ShouldBe(["song.mp3"]);
    }

    [Fact]
    public async Task UploadPhase_ReportsProgressWhileResolving_ThenUploadsContinueFromIt()
    {
        // 3 files in one chunk: a potential update to resolve, an upload and an unchanged file
        _config.GetChunkSize().Returns(3);
        SetupLocalFilesExist();
        _apiClient.CheckSyncAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CheckSyncRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CheckSyncResult
            {
                Records =
                [
                    CreateRecord("changed.mp3", SyncRecordAction.UpdateLocal) with { SongId = 1 },
                    CreateRecord("new.mp3", SyncRecordAction.CreateRemote)
                ],
                Counts = SyncActionCounts.Empty
            });
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [CreateRecord("changed.mp3", SyncRecordAction.UpdateTimestamp) with { SongId = 1 }] });
        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UploadFileResult { Success = true, SongId = 2, Counts = new SyncActionCounts { CreateRemoteCount = 1 } });

        var phases = CreatePhases();
        var progress = new CapturingProgress();
        var files = new List<ScannedFile> { CreateScannedFile("changed.mp3"), CreateScannedFile("new.mp3"), CreateScannedFile("same.mp3") };

        await phases.UploadPhaseAsync(CreateContext(), files, progress);

        // The resolve request settles 1 file, the upload continues from it, then the chunk tops up to 3
        progress.Reports.Select(r => (r.Phase, r.ProcessedFiles)).ShouldBe(
            [("upload", 0), ("resolving", 1), ("upload", 2), ("upload", 3)]);
        progress.Reports.All(r => r.TotalFiles == 3).ShouldBeTrue();
    }

    private void SetupPendingActions(params SyncRecordItem[] records)
    {
        _apiClient.CreatePendingActionsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePendingActionsResult { Records = [.. records] });
    }

    private void SetupDownloadSucceeds()
    {
        _fileOps.FileExists(Arg.Any<string>()).Returns(call => !((string)call[0]).EndsWith(".tmp"));
        _apiClient.DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));
        _apiClient.AcknowledgeActionAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AcknowledgeActionResult { Success = true });
    }

    /// <summary>
    /// A client action that was not performed is reported as an Error linked to its record, instead
    /// of being left unacknowledged (which makes the server reject the commit).
    /// </summary>
    private async Task AssertErrorReported(SyncRecordItem record, string errorMessage)
    {
        await _apiClient.Received(1).ReportSyncErrorAsync(1, 1,
            Arg.Is<ReportSyncErrorCliRequest>(r => r.RecordId == record.Id && r.FilePath == record.FilePath && r.ErrorMessage == errorMessage),
            Arg.Any<CancellationToken>());
        await _apiClient.DidNotReceive().AcknowledgeActionAsync(Arg.Any<long>(), Arg.Any<long>(),
            Arg.Is<AcknowledgeActionRequest>(r => r.RecordIds.Contains(record.Id)), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Makes every local file exist and hashable, as the conflict resolution checksums them.
    /// </summary>
    private void SetupLocalFilesExist()
    {
        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _fileOps.ComputeChecksumAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("AAAA");
    }

    /// <summary>
    /// The upload reports after the one starting the phase, which resets the progress to 0 of every file.
    /// </summary>
    private static List<SyncProgress> ChunkReports(CapturingProgress progress, int totalFiles)
    {
        var uploadReports = progress.Reports.Where(r => r.Phase == "upload").ToList();
        uploadReports[0].ProcessedFiles.ShouldBe(0);
        uploadReports[0].TotalFiles.ShouldBe(totalFiles);
        return uploadReports.Skip(1).ToList();
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
            // The check tells which algorithm to hash the local file with
            Data = action is SyncRecordAction.Conflict or SyncRecordAction.UpdateLocal
                ? JsonSerializer.SerializeToElement(new { serverChecksumAlgorithm = "XxHash128" })
                : null
        };
    }
}