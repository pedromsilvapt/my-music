namespace MyMusic.CLI.Tests.Services.Sync;

using System.Text;
using Microsoft.Extensions.Logging;
using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;
using NSubstitute;
using Shouldly;
using Xunit;

public class SyncActionsDeviceTests
{
    private readonly System.IO.Abstractions.IFileSystem _fileSystem;
    private readonly ISyncApiClient _apiClient;
    private readonly IFileOps _fileOps;
    private readonly IUserPrompt _userPrompt;
    private readonly ILogger<SyncActionsDevice> _logger;

    public SyncActionsDeviceTests()
    {
        _fileSystem = Substitute.For<System.IO.Abstractions.IFileSystem>();
        _apiClient = Substitute.For<ISyncApiClient>();
        _fileOps = Substitute.For<IFileOps>();
        _userPrompt = Substitute.For<IUserPrompt>();
        _userPrompt.PromptConflictResolutionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConflictResolution>>(), Arg.Any<CancellationToken>())
            .Returns(ConflictResolution.Skip);
        _logger = Substitute.For<ILogger<SyncActionsDevice>>();

        _apiClient.AcknowledgeActionAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AcknowledgeActionResult { Success = true }));
    }

    [Fact]
    public async Task ActionCreateRemoteAsync_Success_ReturnsCreatedActionResult()
    {
        var device = CreateDevice();
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

        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UploadFileResult { Success = true, SongId = 1 }));

        var result = await device.ActionCreateRemoteAsync(1, 1, "/music", fileInfo);

        result.Action.ShouldBe("Created");
        result.FilePath.ShouldBe("test.mp3");
        result.Source.ShouldBe("Device");
        result.Reason.ShouldBe("New file");
    }

    [Fact]
    public async Task ActionCreateRemoteAsync_AlwaysUploadsFileToServer()
    {
        var device = CreateDevice();
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

        var uploadResult = new UploadFileResult
        {
            Success = true,
            SongId = 42,
            Counts = new SyncActionCounts { CreateRemoteCount = 1 }
        };
        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(uploadResult));

        var result = await device.ActionCreateRemoteAsync(1, 1, "/music", fileInfo);

        result.Action.ShouldBe("Created");
        result.FilePath.ShouldBe("test.mp3");
        result.SongId.ShouldBe(42);
        result.Counts.ShouldNotBeNull();
        result.Counts.CreateRemoteCount.ShouldBe(1);
        await _apiClient.Received(1).UploadFileAsync(1, 1, Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionCreateRemoteAsync_FileNotFound_ReturnsError()
    {
        var device = CreateDevice();
        var fileInfo = new SyncFileInfo
        {
            Path = "missing.mp3",
            ModifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(false);
        _fileSystem.File.Returns(mockFile);

        var result = await device.ActionCreateRemoteAsync(1, 1, "/music", fileInfo);

        result.Action.ShouldBe("Error");
        result.Reason.ShouldBe("File not found");
    }

    [Fact]
    public async Task ActionUpdateRemoteAsync_Success_ReturnsUpdatedActionResult()
    {
        var device = CreateDevice();
        var fileInfo = new SyncFileInfo
        {
            Path = "test.mp3",
            ModifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Reason = "Modified file"
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        mockFile.OpenRead(Arg.Any<string>()).Returns(callInfo =>
        {
            var stream = new MemoryStream();
            return Substitute.For<System.IO.Abstractions.FileSystemStream>(stream, "test.mp3", false);
        });
        _fileSystem.File.Returns(mockFile);

        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UploadFileResult { Success = true, SongId = 1 }));

        var result = await device.ActionUpdateRemoteAsync(1, 1, "/music", fileInfo);

        result.Action.ShouldBe("Updated");
        result.Source.ShouldBe("Device");
    }

    [Fact]
    public async Task ActionUpdateRemoteAsync_AlwaysUploadsFileToServer()
    {
        var device = CreateDevice();
        var fileInfo = new SyncFileInfo
        {
            Path = "test.mp3",
            ModifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Reason = "Modified file"
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        mockFile.OpenRead(Arg.Any<string>()).Returns(callInfo =>
        {
            var stream = new MemoryStream();
            return Substitute.For<System.IO.Abstractions.FileSystemStream>(stream, "test.mp3", false);
        });
        _fileSystem.File.Returns(mockFile);

        var uploadResult = new UploadFileResult
        {
            Success = true,
            SongId = 42,
            Counts = new SyncActionCounts { UpdateRemoteCount = 1 }
        };
        _apiClient.UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(uploadResult));

        var result = await device.ActionUpdateRemoteAsync(1, 1, "/music", fileInfo);

        result.Action.ShouldBe("Updated");
        result.FilePath.ShouldBe("test.mp3");
        result.SongId.ShouldBe(42);
        result.Counts.ShouldNotBeNull();
        result.Counts.UpdateRemoteCount.ShouldBe(1);
        await _apiClient.Received(1).UploadFileAsync(1, 1, Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionCreateLocalAsync_CreatesParentDirectory()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(false);

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        _apiClient.DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(stream);

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(false);
        _fileSystem.File.Returns(mockFile);

        var result = await device.ActionCreateLocalAsync(1, 1, "/music", 1, "sub/test.mp3", dryRun: false, autoConfirm: true, recordId: 1);

        await _fileOps.Received(1).EnsureDirectoryAsync("/music/sub/test.mp3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionCreateLocalAsync_FileAlreadyExists_ReturnsError()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);

        var result = await device.ActionCreateLocalAsync(1, 1, "/music", 1, "test.mp3", dryRun: false, autoConfirm: false, recordId: 1);

        result.ShouldNotBeNull();
        result.Action.ShouldBe("Error");
        result.ErrorMessage.ShouldBe("File already exists");
        await _apiClient.DidNotReceive().DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionUpdateLocalAsync_FileExists_OverwritesWithoutConfirmation()
    {
        var device = CreateDevice();

        _fileOps.FileExists("/music/test.mp3").Returns(true);
        _fileOps.FileExists(Arg.Any<string>()).Returns(call => (string)call[0] == "/music/test.mp3" || (string)call[0] == "/music/test.mp3.tmp");

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        _apiClient.DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(stream);
        _fileOps.GetModificationTimeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(DateTime.UtcNow);

        var result = await device.ActionUpdateLocalAsync(1, 1, "/music", 1, "test.mp3", dryRun: false, autoConfirm: false, recordId: 1);

        result.ShouldNotBeNull();
        result.Action.ShouldBe("UpdateLocal");
        await _fileOps.Received(1).DeleteFileAsync("/music/test.mp3", Arg.Any<CancellationToken>());
        await _fileOps.Received(1).MoveFileAsync("/music/test.mp3.tmp", "/music/test.mp3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionUpdateLocalAsync_WithLocalSource_CopiesTheLocalFileInsteadOfDownloading()
    {
        // A soundalike of a file uploaded in this session is replaced by that file
        var device = CreateDevice();
        _fileOps.FileExists(Arg.Any<string>()).Returns(call => (string)call[0] is "/music/copy.mp3" or "/music/first.mp3");
        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes("first"));
        mockFile.OpenRead("/music/first.mp3").Returns(Substitute.For<System.IO.Abstractions.FileSystemStream>(sourceStream, "/music/first.mp3", false));
        _fileSystem.File.Returns(mockFile);
        _fileOps.GetModificationTimeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(DateTime.UtcNow);

        var result = await device.ActionUpdateLocalAsync(1, 1, "/music", null, "copy.mp3", dryRun: false, autoConfirm: true, recordId: 1, localSourcePath: "first.mp3");

        // The source file's content is written over the soundalike, and nothing is downloaded
        result!.Action.ShouldBe("UpdateLocal");
        await _fileOps.Received(1).WriteFileAsync("/music/copy.mp3.tmp", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _fileOps.Received(1).MoveFileAsync("/music/copy.mp3.tmp", "/music/copy.mp3", Arg.Any<CancellationToken>());
        await _apiClient.DidNotReceive().DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionUpdateLocalAsync_WithMissingLocalSource_ReportsError()
    {
        var device = CreateDevice();
        _fileOps.FileExists(Arg.Any<string>()).Returns(call => (string)call[0] == "/music/copy.mp3");

        var result = await device.ActionUpdateLocalAsync(1, 7, "/music", null, "copy.mp3", dryRun: false, autoConfirm: true, recordId: 42, localSourcePath: "first.mp3");

        result!.Action.ShouldBe("Error");
        await _fileOps.DidNotReceive().MoveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await AssertFailureReported(recordId: 42, path: "copy.mp3", songId: null);
    }

    [Fact]
    public async Task ActionUpdateLocalAsync_WithLocalSource_DryRun_DoesNotCopy()
    {
        var device = CreateDevice();
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);

        var result = await device.ActionUpdateLocalAsync(1, 1, "/music", null, "copy.mp3", dryRun: true, autoConfirm: true, recordId: 1, localSourcePath: "first.mp3");

        result!.Action.ShouldBe("UpdateLocal");
        await _fileOps.DidNotReceive().WriteFileAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _fileOps.DidNotReceive().MoveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionUpdateLocalAsync_FileDoesNotExist_ReturnsError()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(false);

        var result = await device.ActionUpdateLocalAsync(1, 1, "/music", 1, "test.mp3", dryRun: false, autoConfirm: false, recordId: 1);

        result.ShouldNotBeNull();
        result.Action.ShouldBe("Error");
        result.ErrorMessage.ShouldBe("File not found");
        await _apiClient.DidNotReceive().DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionDeleteLocalAsync_WithUserConfirmation_DeletesFile()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _userPrompt.ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await device.ActionDeleteLocalAsync(1, 1, "/music", 1, "test.mp3", dryRun: false, autoConfirm: false, recordId: 1);

        result.ShouldNotBeNull();
        result.Action.ShouldBe("DeleteLocal");
        await _fileOps.Received(1).DeleteFileAsync("/music/test.mp3", Arg.Any<CancellationToken>());
        await _apiClient.Received(1).AcknowledgeActionAsync(1, Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionDeleteLocalAsync_UserDeclines_ReportsErrorForRecord()
    {
        // The user keeps the file, so the record is reported as an Error and the next sync asks again
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _userPrompt.ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await device.ActionDeleteLocalAsync(1, 7, "/music", 3, "test.mp3", dryRun: false, autoConfirm: false, recordId: 42);

        result!.Action.ShouldBe("Error");
        result.ErrorMessage.ShouldBe("Deletion declined by user");
        await _fileOps.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await AssertFailureReported(recordId: 42, path: "test.mp3", songId: 3);
    }

    [Fact]
    public async Task ActionDeleteLocalAsync_DryRun_DoesNotPrompt()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);

        var result = await device.ActionDeleteLocalAsync(1, 1, "/music", 1, "test.mp3", dryRun: true, autoConfirm: false, recordId: 1);

        result!.Action.ShouldBe("DeleteLocal");
        await _userPrompt.DidNotReceive().ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileOps.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).AcknowledgeActionAsync(1, 1, Arg.Is<AcknowledgeActionRequest>(r => r.RecordIds.Contains(1)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionDeleteLocalAsync_MissingFile_AcknowledgesWithoutDeleting()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(false);

        var result = await device.ActionDeleteLocalAsync(1, 1, "/music", 1, "test.mp3", dryRun: false, autoConfirm: false, recordId: 1);

        result.ShouldBeNull();
        await _apiClient.Received(1).AcknowledgeActionAsync(1, Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionUnlinkAsync_AcknowledgesOnly_DoesNotDeleteFile()
    {
        var device = CreateDevice();

        var result = await device.ActionUnlinkAsync(1, 1, songId: 1, "test.mp3", dryRun: false, recordId: 1);

        result.ShouldNotBeNull();
        result.Action.ShouldBe("Unlink");
        await _fileOps.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).AcknowledgeActionAsync(1, Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionRenameAsync_MoveFileAndCleanup()
    {
        var device = CreateDevice();

        _fileOps.FileExists("/music/old.mp3").Returns(true);
        _fileOps.FileExists(Arg.Any<string>()).Returns(call => (string)call[0] == "/music/old.mp3");

        var result = await device.ActionRenameAsync(1, 1, "/music", "new.mp3", "old.mp3", dryRun: false, recordId: 1);

        result.ShouldNotBeNull();
        result.Action.ShouldBe("Renamed");
        result.Source.ShouldBe("Server");
        _fileOps.Received(1).MoveFileAsync("/music/old.mp3", "/music/new.mp3", Arg.Any<CancellationToken>());
        _fileOps.Received(1).CleanupEmptyParentDirectories("/music/old.mp3", "/music");
    }

    [Fact]
    public async Task ActionConflictAsync_ResolvesConflicts()
    {
        var device = CreateDevice();
        var conflicts = new List<SyncRecordItem>
        {
            new()
            {
                Id = 1,
                FilePath = "conflict.mp3",
                Action = SyncRecordAction.Conflict,
                SongId = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new { localModifiedAt = DateTime.UtcNow, serverModifiedAt = DateTime.UtcNow, serverChecksumAlgorithm = "XxHash128" }),
                Acknowledged = false,
                ProcessedAt = DateTime.UtcNow
            }
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);

        _fileOps.ComputeChecksumAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("checksum");

        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ResolveConflictsResult
            {
                Records =
                [
                    new SyncRecordItem
                    {
                        Id = 1,
                        FilePath = "conflict.mp3",
                        Action = SyncRecordAction.UpdateTimestamp,
                        SongId = 1,
                        Acknowledged = false,
                        ProcessedAt = DateTime.UtcNow
                    }
                ],
            }));

        var result = await device.ActionConflictAsync(1, 1, "/music", conflicts, []);

        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
    }

    [Fact]
    public async Task ActionConflictAsync_DryRun_CallsServerAndPropagatesRecords()
    {
        var device = CreateDevice();
        var conflicts = new List<SyncRecordItem>
        {
            new()
            {
                Id = 1,
                FilePath = "conflict.mp3",
                Action = SyncRecordAction.Conflict,
                SongId = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new { localModifiedAt = DateTime.UtcNow, serverModifiedAt = DateTime.UtcNow, serverChecksumAlgorithm = "XxHash128" }),
                Acknowledged = false,
                ProcessedAt = DateTime.UtcNow
            }
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);

        _fileOps.ComputeChecksumAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("checksum");

        var serverCounts = new SyncActionCounts { UpdateTimestampCount = 1 };
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ResolveConflictsResult
            {
                Records =
                [
                    new SyncRecordItem
                    {
                        Id = 1,
                        FilePath = "conflict.mp3",
                        Action = SyncRecordAction.UpdateTimestamp,
                        SongId = 1,
                        Acknowledged = false,
                        ProcessedAt = DateTime.UtcNow
                    }
                ],
                Counts = serverCounts,
            }));

        var result = await device.ActionConflictAsync(1, 1, "/music", conflicts, []);

        result.Records.Count.ShouldBe(1);
        result.Records[0].Action.ShouldBe(SyncRecordAction.UpdateTimestamp);
        result.Counts.ShouldBe(serverCounts);
        await _fileOps.Received(1).ComputeChecksumAsync("/music/conflict.mp3", "XxHash128", Arg.Any<CancellationToken>());
        await _apiClient.Received(1).ResolveConflictsAsync(1, 1, Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionConflictAsync_SendsChecksumAndAlgorithmInsteadOfFileContent()
    {
        var device = CreateDevice();
        var conflicts = new List<SyncRecordItem> { CreateResolveRecord(1, "conflict.mp3", SyncRecordAction.Conflict) };
        var potentialUpdates = new List<SyncRecordItem> { CreateResolveRecord(2, "changed.mp3", SyncRecordAction.UpdateLocal) };

        SetupLocalFilesExist();
        _fileOps.ComputeChecksumAsync("/music/conflict.mp3", "XxHash128", Arg.Any<CancellationToken>()).Returns("conflict-checksum");
        _fileOps.ComputeChecksumAsync("/music/changed.mp3", "XxHash128", Arg.Any<CancellationToken>()).Returns("changed-checksum");

        ResolveConflictsRequest? sentRequest = null;
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Do<ResolveConflictsRequest>(r => sentRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ResolveConflictsResult { Records = [] }));

        await device.ActionConflictAsync(1, 1, "/music", conflicts, potentialUpdates);

        sentRequest.ShouldNotBeNull();
        var conflict = sentRequest.Conflicts.Single();
        conflict.Checksum.ShouldBe("conflict-checksum");
        conflict.ChecksumAlgorithm.ShouldBe("XxHash128");
        var potentialUpdate = sentRequest.PotentialUpdates.Single();
        potentialUpdate.Checksum.ShouldBe("changed-checksum");
        potentialUpdate.ChecksumAlgorithm.ShouldBe("XxHash128");
    }

    [Fact]
    public async Task ActionConflictAsync_NoChecksumAlgorithmOrUnsupportedOne_SkipsItAndResolvesTheRest()
    {
        var device = CreateDevice();
        var conflicts = new List<SyncRecordItem>
        {
            CreateResolveRecord(1, "no-algorithm.mp3", SyncRecordAction.Conflict) with { Data = System.Text.Json.JsonSerializer.SerializeToElement(new { localModifiedAt = DateTime.UtcNow }) },
            CreateResolveRecord(2, "unsupported.mp3", SyncRecordAction.Conflict, algorithm: "Sha256"),
            CreateResolveRecord(3, "song.mp3", SyncRecordAction.Conflict),
        };

        SetupLocalFilesExist();
        _fileOps.ComputeChecksumAsync(Arg.Any<string>(), "XxHash128", Arg.Any<CancellationToken>()).Returns("checksum");
        _fileOps.ComputeChecksumAsync(Arg.Any<string>(), "Sha256", Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new NotSupportedException("Unsupported checksum algorithm: Sha256"));

        ResolveConflictsRequest? sentRequest = null;
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Do<ResolveConflictsRequest>(r => sentRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ResolveConflictsResult { Records = [] }));

        await device.ActionConflictAsync(1, 1, "/music", conflicts, []);

        sentRequest.ShouldNotBeNull();
        sentRequest.Conflicts.Select(c => c.Path).ShouldBe(["song.mp3"]);
    }

    [Fact]
    public async Task ActionConflictAsync_ManyItems_ChunksRequestsByCountAndAggregatesResults()
    {
        var device = CreateDevice();
        // One more conflict than fits in a request, plus a few potential updates to interleave
        const int conflictCount = SyncActionsDevice.MaxItemsPerResolveChunk + 1;
        const int potentialUpdateCount = 3;

        var conflicts = Enumerable.Range(0, conflictCount)
            .Select(i => CreateResolveRecord(i + 1, $"conflict{i}.mp3", SyncRecordAction.Conflict))
            .ToList();
        var potentialUpdates = Enumerable.Range(0, potentialUpdateCount)
            .Select(i => CreateResolveRecord(1000 + i, $"changed{i}.mp3", SyncRecordAction.UpdateLocal))
            .ToList();

        SetupLocalFilesExist();
        _fileOps.ComputeChecksumAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("checksum");

        var chunkCalls = new List<ResolveConflictsRequest>();
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var req = (ResolveConflictsRequest)callInfo[2];
                chunkCalls.Add(req);
                return Task.FromResult(new ResolveConflictsResult
                {
                    Records = req.Conflicts.Select(c => new SyncRecordItem
                    {
                        Id = c.SongId,
                        FilePath = c.Path,
                        Action = SyncRecordAction.UpdateTimestamp,
                        SongId = c.SongId,
                        Acknowledged = false,
                        ProcessedAt = DateTime.UtcNow
                    }).ToList(),
                    Counts = new SyncActionCounts { UpdateTimestampCount = req.Conflicts.Count + req.PotentialUpdates.Count }
                });
            });

        var result = await device.ActionConflictAsync(1, 1, "/music", conflicts, potentialUpdates);

        // Every request but the last is full, and none exceeds the limit
        chunkCalls.Select(c => c.Conflicts.Count + c.PotentialUpdates.Count)
            .ShouldBe([SyncActionsDevice.MaxItemsPerResolveChunk, conflictCount + potentialUpdateCount - SyncActionsDevice.MaxItemsPerResolveChunk]);
        // The potential updates are interleaved into the first request instead of waiting for all conflicts
        chunkCalls[0].PotentialUpdates.Count.ShouldBe(potentialUpdateCount);
        // All records aggregated into the final result
        result.Records.Count.ShouldBe(conflictCount);
        result.Counts.UpdateTimestampCount.ShouldBe(conflictCount + potentialUpdateCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActionCreateLocalAsync_FileAlreadyExists_ReportsErrorForRecord(bool dryRun)
    {
        var device = CreateDevice();
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);

        await device.ActionCreateLocalAsync(1, 7, "/music", 3, "test.mp3", dryRun, autoConfirm: true, recordId: 42);

        await AssertFailureReported(recordId: 42, path: "test.mp3", songId: 3);
    }

    [Fact]
    public async Task ActionUpdateLocalAsync_FileDoesNotExist_ReportsErrorForRecord()
    {
        var device = CreateDevice();
        _fileOps.FileExists(Arg.Any<string>()).Returns(false);

        await device.ActionUpdateLocalAsync(1, 7, "/music", 3, "test.mp3", dryRun: false, autoConfirm: true, recordId: 42);

        await AssertFailureReported(recordId: 42, path: "test.mp3", songId: 3);
    }

    [Fact]
    public async Task ActionCreateLocalAsync_DownloadFails_ReportsErrorForRecord()
    {
        var device = CreateDevice();
        _fileOps.FileExists(Arg.Any<string>()).Returns(false);
        _apiClient.DownloadSongAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns<Task<Stream>>(_ => throw new HttpRequestException("network down"));

        var result = await device.ActionCreateLocalAsync(1, 7, "/music", 3, "test.mp3", dryRun: false, autoConfirm: true, recordId: 42);

        result!.Action.ShouldBe("Error");
        await AssertFailureReported(recordId: 42, path: "test.mp3", songId: 3);
    }

    [Fact]
    public async Task ActionDeleteLocalAsync_DeleteFails_ReportsErrorForRecord()
    {
        var device = CreateDevice();
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _fileOps.DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("locked"));

        var result = await device.ActionDeleteLocalAsync(1, 7, "/music", 3, "test.mp3", dryRun: false, autoConfirm: true, recordId: 42);

        result!.Action.ShouldBe("Error");
        await AssertFailureReported(recordId: 42, path: "test.mp3", songId: 3);
    }

    [Fact]
    public async Task ActionRenameAsync_MoveFails_ReportsErrorForRecord()
    {
        var device = CreateDevice();
        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _fileOps.MoveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("locked"));

        var result = await device.ActionRenameAsync(1, 7, "/music", "new.mp3", "old.mp3", dryRun: false, recordId: 42);

        result!.Action.ShouldBe("Error");
        await AssertFailureReported(recordId: 42, path: "new.mp3", songId: null);
    }

    /// <summary>
    /// A failed client action is reported as an Error linked to its record (which the server
    /// acknowledges), instead of being acknowledged as if it had succeeded.
    /// </summary>
    private async Task AssertFailureReported(long recordId, string path, long? songId)
    {
        await _apiClient.Received(1).ReportSyncErrorAsync(1, 7,
            Arg.Is<ReportSyncErrorCliRequest>(r => r.RecordId == recordId && r.FilePath == path && r.SongId == songId),
            Arg.Any<CancellationToken>());
        await _apiClient.DidNotReceive().AcknowledgeActionAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionConflictAsync_UnreadableFile_SkipsItAndResolvesTheRest()
    {
        var device = CreateDevice();
        var data = System.Text.Json.JsonSerializer.SerializeToElement(new { localModifiedAt = DateTime.UtcNow, serverModifiedAt = DateTime.UtcNow, lastSyncedAt = DateTime.UtcNow, serverChecksumAlgorithm = "XxHash128" });
        var conflicts = new List<SyncRecordItem>
        {
            new() { Id = 1, FilePath = "unreadable.mp3", Action = SyncRecordAction.Conflict, SongId = 1, Data = data, Acknowledged = false, ProcessedAt = DateTime.UtcNow },
            new() { Id = 2, FilePath = "song.mp3", Action = SyncRecordAction.Conflict, SongId = 2, Data = data, Acknowledged = false, ProcessedAt = DateTime.UtcNow },
        };
        var potentialUpdates = new List<SyncRecordItem>
        {
            new() { Id = 3, FilePath = "unreadable-update.mp3", Action = SyncRecordAction.UpdateLocal, SongId = 3, Data = data, Acknowledged = false, ProcessedAt = DateTime.UtcNow },
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);

        _fileOps.ComputeChecksumAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("checksum");
        _fileOps.ComputeChecksumAsync(Arg.Is<string>(p => p.EndsWith("unreadable.mp3") || p.EndsWith("unreadable-update.mp3")), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new IOException("Permission denied"));

        ResolveConflictsRequest? sentRequest = null;
        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Do<ResolveConflictsRequest>(r => sentRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ResolveConflictsResult
            {
                Records = [new SyncRecordItem { Id = 2, FilePath = "song.mp3", Action = SyncRecordAction.UpdateTimestamp, SongId = 2, Acknowledged = false, ProcessedAt = DateTime.UtcNow }],
            }));

        var result = await device.ActionConflictAsync(1, 1, "/music", conflicts, potentialUpdates);

        sentRequest.ShouldNotBeNull();
        sentRequest.Conflicts.Select(c => c.Path).ShouldBe(["song.mp3"]);
        sentRequest.PotentialUpdates.ShouldBeEmpty();
        result.Records.Single().FilePath.ShouldBe("song.mp3");
    }

    private void SetupLocalFilesExist()
    {
        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);
    }

    private static SyncRecordItem CreateResolveRecord(long id, string path, SyncRecordAction action, string algorithm = "XxHash128") => new()
    {
        Id = id,
        FilePath = path,
        Action = action,
        SongId = id,
        Data = System.Text.Json.JsonSerializer.SerializeToElement(new { localModifiedAt = DateTime.UtcNow, serverModifiedAt = DateTime.UtcNow, lastSyncedAt = DateTime.UtcNow, serverChecksumAlgorithm = algorithm }),
        Acknowledged = false,
        ProcessedAt = DateTime.UtcNow
    };

    private void SetupRealConflict(SyncRecordItem resolvedConflict)
    {
        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        mockFile.OpenRead(Arg.Any<string>()).Returns(_ => Substitute.For<System.IO.Abstractions.FileSystemStream>(new MemoryStream(), "song.mp3", false));
        _fileSystem.File.Returns(mockFile);

        _fileOps.ComputeChecksumAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("checksum");

        _apiClient.ResolveConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [resolvedConflict], Counts = new SyncActionCounts { ConflictCount = 1 } });
    }

    [Fact]
    public async Task ActionConflictAsync_UserChoosesDownload_AsksTheServerForTheResolvingRecords()
    {
        var device = CreateDevice();
        var resolvedConflict = CreateResolveRecord(10, "song.mp3", SyncRecordAction.Conflict);
        SetupRealConflict(resolvedConflict);
        var updateLocal = CreateResolveRecord(11, "song.mp3", SyncRecordAction.UpdateLocal) with { ResolvesConflictRecordId = 10 };
        _userPrompt.PromptConflictResolutionAsync("song.mp3", Arg.Any<IReadOnlyList<ConflictResolution>>(), Arg.Any<CancellationToken>())
            .Returns(ConflictResolution.Download);
        _apiClient.ChooseConflictsAsync(1, 1, Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [updateLocal], Counts = new SyncActionCounts { UpdateLocalCount = 1, ConflictCount = -1 } });

        var result = await device.ActionConflictAsync(1, 1, "/music", [CreateResolveRecord(1, "song.mp3", SyncRecordAction.Conflict)], []);

        await _apiClient.Received(1).ChooseConflictsAsync(1, 1, Arg.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 10 })), Arg.Any<CancellationToken>());
        await _apiClient.DidNotReceive().UploadFileAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>());
        result.Records.ShouldBe([resolvedConflict, updateLocal]);
        result.Counts.UpdateLocalCount.ShouldBe(1);
        result.Counts.ConflictCount.ShouldBe(0);
        result.UploadedPaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task ActionConflictAsync_UserChoosesUpload_UploadsTheLocalFileResolvingTheConflict()
    {
        var device = CreateDevice();
        var resolvedConflict = CreateResolveRecord(10, "song.mp3", SyncRecordAction.Conflict);
        SetupRealConflict(resolvedConflict);
        var updateRemote = CreateResolveRecord(11, "song.mp3", SyncRecordAction.UpdateRemote) with { ResolvesConflictRecordId = 10 };
        _userPrompt.PromptConflictResolutionAsync("song.mp3", Arg.Any<IReadOnlyList<ConflictResolution>>(), Arg.Any<CancellationToken>())
            .Returns(ConflictResolution.Upload);
        _apiClient.UploadFileAsync(1, 1, Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UploadFileResult { Success = true, SongId = 10, Records = [updateRemote], Counts = new SyncActionCounts { UpdateRemoteCount = 1, ConflictCount = -1 } });
        var modifiedAt = new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var files = new List<SyncFileInfo> { new() { Path = "song.mp3", ModifiedAt = modifiedAt, CreatedAt = createdAt } };

        var result = await device.ActionConflictAsync(1, 1, "/music", [CreateResolveRecord(1, "song.mp3", SyncRecordAction.Conflict)], [], files: files);

        await _apiClient.Received(1).UploadFileAsync(1, 1, Arg.Is<UploadFileRequest>(r =>
            r.Path == "song.mp3" && r.ResolvesConflictRecordId == 10
            && r.ModifiedAt == modifiedAt.ToString("O") && r.CreatedAt == createdAt.ToString("O")), Arg.Any<CancellationToken>());
        await _apiClient.DidNotReceive().ChooseConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>());
        result.Records.ShouldBe([resolvedConflict, updateRemote]);
        result.Counts.UpdateRemoteCount.ShouldBe(1);
        result.Counts.ConflictCount.ShouldBe(0);
        result.UploadedPaths.ShouldBe(["song.mp3"]);
    }

    [Fact]
    public async Task ActionConflictAsync_UploadFails_LeavesTheConflictUnresolved()
    {
        var device = CreateDevice();
        var resolvedConflict = CreateResolveRecord(10, "song.mp3", SyncRecordAction.Conflict);
        SetupRealConflict(resolvedConflict);
        _userPrompt.PromptConflictResolutionAsync("song.mp3", Arg.Any<IReadOnlyList<ConflictResolution>>(), Arg.Any<CancellationToken>())
            .Returns(ConflictResolution.Upload);
        _apiClient.UploadFileAsync(1, 1, Arg.Any<UploadFileRequest>(), Arg.Any<CancellationToken>())
            .Returns<UploadFileResult>(_ => throw new HttpRequestException("Network error"));

        var result = await device.ActionConflictAsync(1, 1, "/music", [CreateResolveRecord(1, "song.mp3", SyncRecordAction.Conflict)], []);

        result.Records.ShouldBe([resolvedConflict]);
        result.UploadedPaths.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ConflictResolution.Download)]
    [InlineData(ConflictResolution.Skip)]
    public async Task ActionConflictAsync_ResolutionGivenInOptions_DoesNotAsk(ConflictResolution conflicts)
    {
        var device = CreateDevice();
        SetupRealConflict(CreateResolveRecord(10, "song.mp3", SyncRecordAction.Conflict));
        _apiClient.ChooseConflictsAsync(1, 1, Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveConflictsResult { Records = [] });

        await device.ActionConflictAsync(1, 1, "/music", [CreateResolveRecord(1, "song.mp3", SyncRecordAction.Conflict)], [],
            options: new SyncOptions { DryRun = true, Conflicts = conflicts });

        await _userPrompt.DidNotReceive().PromptConflictResolutionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ConflictResolution>>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(conflicts == ConflictResolution.Download ? 1 : 0)
            .ChooseConflictsAsync(1, 1, Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SyncDirection.Up, new[] { ConflictResolution.Upload, ConflictResolution.Skip })]
    [InlineData(SyncDirection.Down, new[] { ConflictResolution.Download, ConflictResolution.Skip })]
    [InlineData(SyncDirection.Both, new[] { ConflictResolution.Upload, ConflictResolution.Download, ConflictResolution.Skip })]
    public async Task ActionConflictAsync_OnlyOffersTheChoicesTheDirectionCanApply(SyncDirection direction, ConflictResolution[] choices)
    {
        var device = CreateDevice();
        SetupRealConflict(CreateResolveRecord(10, "song.mp3", SyncRecordAction.Conflict));

        await device.ActionConflictAsync(1, 1, "/music", [CreateResolveRecord(1, "song.mp3", SyncRecordAction.Conflict)], [],
            options: new SyncOptions { Direction = direction });

        await _userPrompt.Received(1).PromptConflictResolutionAsync("song.mp3",
            Arg.Is<IReadOnlyList<ConflictResolution>>(c => c.SequenceEqual(choices)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionConflictAsync_DirectionUp_DownloadGivenInOptions_SkipsTheConflict()
    {
        var device = CreateDevice();
        SetupRealConflict(CreateResolveRecord(10, "song.mp3", SyncRecordAction.Conflict));

        await device.ActionConflictAsync(1, 1, "/music", [CreateResolveRecord(1, "song.mp3", SyncRecordAction.Conflict)], [],
            options: new SyncOptions { Direction = SyncDirection.Up, Conflicts = ConflictResolution.Download });

        await _apiClient.DidNotReceive().ChooseConflictsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>());
    }

    private SyncActionsDevice CreateDevice() => new(_fileOps, _apiClient, _userPrompt, _fileSystem, _logger);
}