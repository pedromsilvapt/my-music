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
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var repoPath = "/data";

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed, repoPath);
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
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var repoPath = "/data";

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.Cancelled, repoPath);
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
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var repoPath = "/data";

        var session = CreateSession(scenario.DbContext, device, SyncSessionStatus.InProgress, repoPath);
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
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        var repoPath = "/data";

        CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed, repoPath);
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
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
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
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed, "/nonexistent");

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);
    }

    [Fact]
    public async Task CleanupStaleDirectories_HandlesSessionsWithoutRepositoryPath()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);
        CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed, repositoryPath: null);

        var service = CreateService(mockFs);

        await service.CleanupStaleDirectoriesAsync(scenario.DbContext, CancellationToken.None);
    }

    [Fact]
    public async Task CleanupStaleDirectories_DeletesMultipleStaleDirsAcrossRepos()
    {
        var scenario = new Scenario();
        var mockFs = (MockFileSystem)scenario.FileSystem;
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id);

        var repo1 = "/data1";
        var repo2 = "/data2";
        var session1 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Cancelled, repo1);
        var session2 = CreateSession(scenario.DbContext, device, SyncSessionStatus.Committed, repo2);

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

    private Device CreateDevice(MusicDbContext db, long ownerId)
    {
        var device = new Device
        {
            Name = $"Device-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            Songs = []
        };
        db.Add(device);
        db.SaveChanges();
        return device;
    }

    private DeviceSyncSession CreateSession(MusicDbContext db, Device device, SyncSessionStatus status, string? repositoryPath)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = DateTime.UtcNow,
            Status = status,
            IsDryRun = false,
            RepositoryPath = repositoryPath,
            Records = []
        };
        db.DeviceSyncSessions.Add(session);
        db.SaveChanges();
        return session;
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