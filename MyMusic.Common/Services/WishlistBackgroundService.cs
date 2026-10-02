using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.BackgroundJobs;

namespace MyMusic.Common.Services;

public class WishlistBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<Config> config,
    ILogger<WishlistBackgroundService> logger) : BackgroundService, IQueuedBackgroundJob
{
    public string Key => "wishlist";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        var intervalMinutes = config.Value.WishlistCheckIntervalMinutes;
        if (intervalMinutes <= 0)
        {
            logger.LogInformation("Wishlist background service disabled (interval set to {Interval})", intervalMinutes);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var wishlistService = scope.ServiceProvider.GetRequiredService<IWishlistService>();

                await wishlistService.CheckForUpdatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during wishlist check");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    /// <summary>
    /// Active items are the ones checked on every cycle. Successful checks are not recorded, so nothing is processed.
    /// </summary>
    public async Task<BackgroundJobCounters> GetCountersAsync(MusicDbContext db, long userId,
        CancellationToken cancellationToken)
    {
        var owned = db.WishlistItems.Where(w => w.OwnerId == userId);

        var queued = await owned.CountAsync(w => w.Status == WishlistItemStatus.Active, cancellationToken);
        var failed = await owned.CountAsync(w => w.ContinuousFailedCount > 0, cancellationToken);

        return new BackgroundJobCounters(queued, null, failed);
    }

    public Task<BackgroundJobFailurePage> GetFailuresAsync(MusicDbContext db, long userId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var failing = db.WishlistItems
            .Include(w => w.Source)
            .Where(w => w.OwnerId == userId && w.ContinuousFailedCount > 0)
            .OrderByDescending(w => w.UpdatedAt)
            .ThenByDescending(w => w.Id)
            .AsNoTracking();

        return BackgroundJobFailurePage.FromQueryAsync(failing, page, pageSize, w => new BackgroundJobFailure(
            w.Id.ToString(CultureInfo.InvariantCulture),
            w.Query,
            w.LastErrorMessage,
            w.UpdatedAt,
            [
                new("WishlistItemId", w.Id.ToString(CultureInfo.InvariantCulture)),
                new("Query", w.Query),
                new("Filter", w.Filter),
                new("Source", w.Source.Name),
                new("SourceId", w.SourceId.ToString(CultureInfo.InvariantCulture)),
                new("Status", w.Status.ToString()),
                new("ContinuousFailedCount", w.ContinuousFailedCount.ToString(CultureInfo.InvariantCulture)),
                new("CreatedAt", w.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
                new("UpdatedAt", w.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)),
            ]), cancellationToken);
    }
}
