using System.IO.Abstractions.TestingHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class StagingDirectoryCleanupServiceSpecs
{
    [Fact]
    public async Task CleanupStaleDirectories_DeletesDirectoriesForCommittedSessions()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        var repoPath = "/data";

        var session = scenario.CreateSession(device, status: SyncSessionStatus.Committed, repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";
        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);

        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupStaleDirectories_DeletesDirectoriesForCancelledSessions()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        var repoPath = "/data";

        var session = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled, repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";
        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);

        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupStaleDirectories_DoesNotDeleteDirectoriesForInProgressSessions()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        var repoPath = "/data";

        var session = scenario.CreateSession(device, status: SyncSessionStatus.InProgress, repositoryPath: repoPath);
        var stagingDir = $"{repoPath}/.temp/sync-{session.Id}";
        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);

        mockFs.Directory.Exists(stagingDir).ShouldBeTrue();
    }

    [Fact]
    public async Task CleanupStaleDirectories_IgnoresDirectoriesNotMatchingSessionPattern()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        var repoPath = "/data";

        scenario.CreateSession(device, status: SyncSessionStatus.Committed, repositoryPath: repoPath);
        mockFs.AddDirectory($"{repoPath}/.temp/other-dir");
        mockFs.AddFile($"{repoPath}/.temp/other-dir/file.txt", new MockFileData("data"));

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);

        mockFs.Directory.Exists($"{repoPath}/.temp/other-dir").ShouldBeTrue();
    }

    [Fact]
    public async Task CleanupStaleDirectories_IgnoresDirectoriesForUnknownSessionIds()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        var repoPath = "/data";

        mockFs.AddDirectory($"{repoPath}/.temp/sync-99999");
        mockFs.AddFile($"{repoPath}/.temp/sync-99999/file.mp3", new MockFileData("data"));

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);

        mockFs.Directory.Exists($"{repoPath}/.temp/sync-99999").ShouldBeTrue();
    }

    [Fact]
    public async Task CleanupStaleDirectories_HandlesMissingTempDir()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        scenario.CreateSession(device, status: SyncSessionStatus.Committed, repositoryPath: "/nonexistent");

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);
    }

    [Fact]
    public async Task CleanupStaleDirectories_HandlesSessionsWithoutRepositoryPath()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();
        scenario.CreateSession(device, status: SyncSessionStatus.Committed, repositoryPath: null);

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);
    }

    [Fact]
    public async Task CleanupStaleDirectories_DeletesMultipleStaleDirsAcrossRepos()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = scenario.CreateDevice();

        var repo1 = "/data1";
        var repo2 = "/data2";
        var session1 = scenario.CreateSession(device, status: SyncSessionStatus.Cancelled, repositoryPath: repo1);
        var session2 = scenario.CreateSession(device, status: SyncSessionStatus.Committed, repositoryPath: repo2);

        var stagingDir1 = $"{repo1}/.temp/sync-{session1.Id}";
        var stagingDir2 = $"{repo2}/.temp/sync-{session2.Id}";
        mockFs.AddDirectory(stagingDir1);
        mockFs.AddFile($"{stagingDir1}/file.mp3", new MockFileData("data"));
        mockFs.AddDirectory(stagingDir2);
        mockFs.AddFile($"{stagingDir2}/file.mp3", new MockFileData("data"));

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);

        mockFs.Directory.Exists(stagingDir1).ShouldBeFalse();
        mockFs.Directory.Exists(stagingDir2).ShouldBeFalse();
    }

    [Fact]
    public void DeleteStagingDirectory_DeletesExistingStagingDir()
    {
        var mockFs = new MockFileSystem();
        var logger = Substitute.For<ILogger>();
        var repoPath = "/data";
        var sessionId = 42L;
        var stagingDir = $"{repoPath}/.temp/sync-{sessionId}";

        mockFs.AddDirectory(stagingDir);
        mockFs.AddFile($"{stagingDir}/file.mp3", new MockFileData("data"));

        var result = StagingDirectoryCleanupService.DeleteStagingDirectory(mockFs, repoPath, sessionId, logger);

        result.ShouldBeTrue();
        mockFs.Directory.Exists(stagingDir).ShouldBeFalse();
    }

    [Fact]
    public void DeleteStagingDirectory_SkipsWhenNoRepositoryPath()
    {
        var mockFs = new MockFileSystem();
        var logger = Substitute.For<ILogger>();

        var result = StagingDirectoryCleanupService.DeleteStagingDirectory(mockFs, null, 42, logger);

        result.ShouldBeFalse();
    }

    [Fact]
    public void DeleteStagingDirectory_SkipsWhenStagingDirDoesNotExist()
    {
        var mockFs = new MockFileSystem();
        var logger = Substitute.For<ILogger>();

        var result = StagingDirectoryCleanupService.DeleteStagingDirectory(mockFs, "/data", 42, logger);

        result.ShouldBeFalse();
    }

    private StagingDirectoryCleanupService CreateService(MockFileSystem mockFs)
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        return new StagingDirectoryCleanupService(
            scopeFactory,
            mockFs,
            Substitute.For<ILogger<StagingDirectoryCleanupService>>());
    }
}