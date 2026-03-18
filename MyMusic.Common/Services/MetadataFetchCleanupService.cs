using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

/// <summary>
/// Background service that periodically cleans up expired auto-fetched metadata records.
/// Runs daily to mark records older than 30 days as Expired and removes old failed records.
/// </summary>
public class MetadataFetchCleanupService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<MetadataFetchCleanupService> logger)
    : BackgroundService
{
    // Run cleanup once per day
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromDays(1);

    // Records older than 30 days are considered expired
    private readonly TimeSpan _expirationWindow = TimeSpan.FromDays(30);

    // Delete failed records older than 7 days
    private readonly TimeSpan _failedRecordRetention = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Metadata fetch cleanup service started. Cleanup interval: {Interval}, Expiration window: {Window}",
            _cleanupInterval,
            _expirationWindow);

        // Run initial cleanup on startup
        await PerformCleanupAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    await PerformCleanupAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Metadata fetch cleanup service stopping");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during metadata cleanup");
            }
        }
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();

            var cutoffDate = DateTime.UtcNow.Subtract(_expirationWindow);
            var failedCutoffDate = DateTime.UtcNow.Subtract(_failedRecordRetention);

            // Mark old pending metadata as expired
            var expiredCount = await MarkExpiredMetadataAsync(context, cutoffDate, cancellationToken);

            // Delete old failed metadata records
            var deletedFailedCount = await DeleteOldFailedMetadataAsync(context, failedCutoffDate, cancellationToken);

            // Delete old expired metadata records (keep for 30 days after expiration)
            var deletedExpiredCount = await DeleteOldExpiredMetadataAsync(context, cutoffDate, cancellationToken);

            // Clean up old completed tasks
            var deletedTasksCount = await DeleteOldCompletedTasksAsync(context, cutoffDate, cancellationToken);

            if (expiredCount > 0 || deletedFailedCount > 0 || deletedExpiredCount > 0 || deletedTasksCount > 0)
            {
                logger.LogInformation(
                    "Metadata cleanup completed: {ExpiredCount} records marked as expired, " +
                    "{DeletedFailed} failed records deleted, {DeletedExpired} expired records deleted, " +
                    "{DeletedTasks} old tasks deleted",
                    expiredCount,
                    deletedFailedCount,
                    deletedExpiredCount,
                    deletedTasksCount);
            }
            else
            {
                logger.LogDebug("Metadata cleanup completed. No records needed cleanup");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing metadata cleanup");
            throw;
        }
    }

    private async Task<int> MarkExpiredMetadataAsync(
        MusicDbContext context,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        var expiredRecords = await context.AutoFetchedMetadata
            .Where(m => m.Status == AutoFetchStatus.Pending && m.FetchedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        foreach (var record in expiredRecords)
        {
            record.Status = AutoFetchStatus.Expired;
        }

        if (expiredRecords.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return expiredRecords.Count;
    }

    private async Task<int> DeleteOldFailedMetadataAsync(
        MusicDbContext context,
        DateTime failedCutoffDate,
        CancellationToken cancellationToken)
    {
        var oldFailedRecords = await context.AutoFetchedMetadata
            .Where(m => m.Status == AutoFetchStatus.Failed && m.FetchedAt < failedCutoffDate)
            .ToListAsync(cancellationToken);

        if (oldFailedRecords.Count > 0)
        {
            context.AutoFetchedMetadata.RemoveRange(oldFailedRecords);
            await context.SaveChangesAsync(cancellationToken);
        }

        return oldFailedRecords.Count;
    }

    private async Task<int> DeleteOldExpiredMetadataAsync(
        MusicDbContext context,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        // Delete expired records that are older than the cutoff date
        // This gives a 30-day window after expiration for any auditing/debugging
        var oldExpiredRecords = await context.AutoFetchedMetadata
            .Where(m => m.Status == AutoFetchStatus.Expired && m.FetchedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        if (oldExpiredRecords.Count > 0)
        {
            context.AutoFetchedMetadata.RemoveRange(oldExpiredRecords);
            await context.SaveChangesAsync(cancellationToken);
        }

        return oldExpiredRecords.Count;
    }

    private async Task<int> DeleteOldCompletedTasksAsync(
        MusicDbContext context,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        var oldTasks = await context.MetadataFetchTasks
            .Where(t => (t.Status == MetadataFetchStatus.Completed || t.Status == MetadataFetchStatus.Failed)
                        && t.CompletedAt.HasValue
                        && t.CompletedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        if (oldTasks.Count > 0)
        {
            context.MetadataFetchTasks.RemoveRange(oldTasks);
            await context.SaveChangesAsync(cancellationToken);
        }

        return oldTasks.Count;
    }
}
