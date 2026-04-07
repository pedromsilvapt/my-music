using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Targets;

namespace MyMusic.Common.Services;

/// <summary>
/// One-shot background service that backfills the bitrate field for songs where it is null.
/// Runs once on startup (after a 30s delay), processes all null-bitrate songs in batches, then stops.
/// Controlled by Config.BitrateBackfillEnabled — does nothing when disabled.
/// </summary>
public class BitrateBackfillService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<Config> config,
    IFileSystem fileSystem,
    ILogger<BitrateBackfillService> logger) : BackgroundService
{
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        if (!config.Value.BitrateBackfillEnabled)
        {
            logger.LogInformation("Bitrate backfill service disabled (BitrateBackfillEnabled is false)");
            return;
        }

        logger.LogInformation("Bitrate backfill service starting");

        var totalProcessed = 0;
        var totalErrors = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            int errors;

            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();

                (processed, errors) = await BackfillBatchAsync(
                    context, config.Value.MusicRepositoryPath, fileSystem, logger, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during bitrate backfill batch");
                break;
            }

            totalProcessed += processed;
            totalErrors += errors;

            if (processed == 0)
            {
                break;
            }
        }

        logger.LogInformation(
            "Bitrate backfill service completed. Processed: {Processed}, Errors: {Errors}",
            totalProcessed,
            totalErrors);
    }

    public static async Task<(int processed, int errors)> BackfillBatchAsync(
        MusicDbContext context,
        string repositoryPath,
        IFileSystem fs,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var songs = await context.Songs
            .Where(s => s.Bitrate == null)
            .OrderBy(s => s.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (songs.Count == 0)
        {
            return (0, 0);
        }

        var errors = 0;

        foreach (var song in songs)
        {
            var filePath = fs.Path.Combine(repositoryPath, song.RepositoryPath);

            if (!fs.File.Exists(filePath))
            {
                logger.LogWarning("File not found for song {SongId}: {Path}", song.Id, filePath);
                errors++;
                continue;
            }

            try
            {
                var fileInfo = new FileSystemFileAbstraction(fs.FileInfo.New(filePath));
                using var tfile = TagLib.File.Create(fileInfo);
                var bitrate = tfile.Properties.AudioBitrate;

                if (bitrate > 0)
                {
                    song.Bitrate = bitrate;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read bitrate for song {SongId}: {Path}", song.Id, filePath);
                errors++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bitrate backfill batch: processed {Count} songs, {Errors} errors",
            songs.Count,
            errors);

        return (songs.Count, errors);
    }
}
