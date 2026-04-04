using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMusic.Common.Services;

public class WishlistBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<Config> config,
    ILogger<WishlistBackgroundService> logger) : BackgroundService
{
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
}