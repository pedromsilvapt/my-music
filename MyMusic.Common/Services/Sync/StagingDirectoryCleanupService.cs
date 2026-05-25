using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public partial class StagingDirectoryCleanupService(
    IServiceScopeFactory serviceScopeFactory,
    IFileSystem fileSystem,
    ILogger<StagingDirectoryCleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Staging directory cleanup service started");

        await CleanupStaleDirectoriesAsync(stoppingToken);

        logger.LogInformation("Staging directory cleanup service finished initial cleanup");
    }

    public async Task CleanupStaleDirectoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
            await CleanupStaleDirectoriesAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing stale staging directory cleanup");
        }
    }

    public async Task CleanupStaleDirectoriesAsync(MusicDbContext context, CancellationToken cancellationToken)
    {
        var nonInProgressSessions = await context.DeviceSyncSessions
            .Where(s => s.Status != SyncSessionStatus.InProgress && s.RepositoryPath != null)
            .Select(s => new { s.Id, s.RepositoryPath })
            .ToListAsync(cancellationToken);

        var knownSessionIds = nonInProgressSessions
            .Select(s => s.Id)
            .ToHashSet();

        var repoPaths = nonInProgressSessions
            .Select(s => s.RepositoryPath!)
            .Distinct()
            .ToList();

        var deletedCount = 0;

        foreach (var repoPath in repoPaths)
        {
            var tempDir = fileSystem.Path.Combine(repoPath, ".temp");
            if (!fileSystem.Directory.Exists(tempDir))
            {
                continue;
            }

            string[] syncDirs;
            try
            {
                syncDirs = fileSystem.Directory.GetDirectories(tempDir, "sync-*");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to enumerate staging directories in {TempDir}", tempDir);
                continue;
            }

            foreach (var syncDir in syncDirs)
            {
                var dirName = fileSystem.Path.GetFileName(syncDir);
                var match = SyncDirPattern().Match(dirName);
                if (!match.Success)
                {
                    continue;
                }

                var sessionId = long.Parse(match.Groups[1].Value);

                if (!knownSessionIds.Contains(sessionId))
                {
                    continue;
                }

                try
                {
                    fileSystem.Directory.Delete(syncDir, true);
                    deletedCount++;
                    logger.LogInformation(
                        "Deleted stale staging directory {StagingDir} for session {SessionId}",
                        syncDir, sessionId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to delete stale staging directory {StagingDir} for session {SessionId}",
                        syncDir, sessionId);
                }
            }
        }

        if (deletedCount > 0)
        {
            logger.LogInformation("Stale staging directory cleanup completed: {DeletedCount} directories removed", deletedCount);
        }
        else
        {
            logger.LogDebug("Stale staging directory cleanup completed. No directories needed cleanup");
        }
    }

    public static bool DeleteStagingDirectory(IFileSystem fs, string? repositoryPath, long sessionId, ILogger logger)
    {
        var stagingDir = repositoryPath != null
            ? fs.Path.Combine(repositoryPath, ".temp", $"sync-{sessionId}")
            : null;

        if (stagingDir == null || !fs.Directory.Exists(stagingDir))
        {
            return false;
        }

        try
        {
            fs.Directory.Delete(stagingDir, true);
            logger.LogInformation("Deleted staging directory {StagingDir}", stagingDir);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete staging directory {StagingDir}", stagingDir);
            return false;
        }
    }

    [GeneratedRegex(@"^sync-(\d+)$")]
    private static partial Regex SyncDirPattern();
}