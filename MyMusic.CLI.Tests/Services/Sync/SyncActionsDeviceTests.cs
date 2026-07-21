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
    public async Task ActionDeleteAsync_WithUserConfirmation_DeletesFile()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _userPrompt.ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await device.ActionDeleteAsync(1, 1, "/music", 1, "test.mp3", dryRun: false, autoConfirm: false, recordId: 1);

        result.ShouldNotBeNull();
        result.Action.ShouldBe("Delete");
        await _fileOps.Received(1).DeleteFileAsync("/music/test.mp3", Arg.Any<CancellationToken>());
        await _apiClient.Received(1).AcknowledgeActionAsync(1, Arg.Any<long>(), Arg.Any<AcknowledgeActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionDeleteAsync_UserCancels_ReturnsNull()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(true);
        _userPrompt.ConfirmDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await device.ActionDeleteAsync(1, 1, "/music", 1, "test.mp3", dryRun: false, autoConfirm: false, recordId: 1);

        result.ShouldBeNull();
        await _fileOps.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionDeleteAsync_MissingFile_AcknowledgesWithoutDeleting()
    {
        var device = CreateDevice();

        _fileOps.FileExists(Arg.Any<string>()).Returns(false);

        var result = await device.ActionDeleteAsync(1, 1, "/music", 1, "test.mp3", dryRun: false, autoConfirm: false, recordId: 1);

        result.ShouldBeNull();
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
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new { localModifiedAt = DateTime.UtcNow, serverModifiedAt = DateTime.UtcNow }),
                Acknowledged = false,
                ProcessedAt = DateTime.UtcNow
            }
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);

        _fileOps.ReadFileBase64Async(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("base64content");

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
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new { localModifiedAt = DateTime.UtcNow, serverModifiedAt = DateTime.UtcNow }),
                Acknowledged = false,
                ProcessedAt = DateTime.UtcNow
            }
        };

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);

        _fileOps.ReadFileBase64Async(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("base64content");

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
        await _fileOps.Received(1).ReadFileBase64Async(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _apiClient.Received(1).ResolveConflictsAsync(1, 1, Arg.Any<ResolveConflictsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActionConflictAsync_LargePayload_ChunksRequestsAndAggregatesResults()
    {
        var device = CreateDevice();
        // Generate enough conflicts that combined base64 payload exceeds the chunk threshold.
        // The default chunk threshold is ~20 MB of base64; each conflict carries a 7 MB base64 string,
        // so 4 conflicts => 28 MB => must split into at least 2 chunks.
        var bigBase64 = new string('A', 7_000_000);
        const int conflictCount = 4;

        var conflicts = new List<SyncRecordItem>();
        for (var i = 0; i < conflictCount; i++)
        {
            conflicts.Add(new SyncRecordItem
            {
                Id = i + 1,
                FilePath = $"conflict{i}.mp3",
                Action = SyncRecordAction.Conflict,
                SongId = i + 1,
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new { localModifiedAt = DateTime.UtcNow, serverModifiedAt = DateTime.UtcNow }),
                Acknowledged = false,
                ProcessedAt = DateTime.UtcNow
            });
        }

        var mockFile = Substitute.For<System.IO.Abstractions.IFile>();
        mockFile.Exists(Arg.Any<string>()).Returns(true);
        _fileSystem.File.Returns(mockFile);

        _fileOps.ReadFileBase64Async(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(bigBase64);

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

        var result = await device.ActionConflictAsync(1, 1, "/music", conflicts, []);

        // Should have made more than one request (chunking kicked in)
        chunkCalls.Count.ShouldBeGreaterThan(1);
        // Each chunk's total base64 payload must respect the threshold
        foreach (var chunk in chunkCalls)
        {
            var chunkBytes = chunk.Conflicts.Sum(c => c.FileContentBase64.Length)
                                + chunk.PotentialUpdates.Sum(p => p.FileContentBase64.Length);
            chunkBytes.ShouldBeLessThanOrEqualTo(20_000_000);
        }
        // All conflicts were processed across chunks
        chunkCalls.Sum(c => c.Conflicts.Count).ShouldBe(conflictCount);
        // All records aggregated into the final result
        result.Records.Count.ShouldBe(conflictCount);
        result.Counts.UpdateTimestampCount.ShouldBe(conflictCount);
    }

    private SyncActionsDevice CreateDevice() => new(_fileOps, _apiClient, _userPrompt, _fileSystem, _logger);
}