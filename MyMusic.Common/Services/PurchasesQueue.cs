using System.Globalization;
using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.BackgroundJobs;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Sources;
using MyMusic.Common.Targets;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.Services;

public class PurchasesQueue(IServiceScopeFactory serviceScopeFactory)
    : BackgroundService, IQueuedBackgroundJob
{
    public PurchasesScheduler Scheduler { get; } = new(serviceScopeFactory, 1);

    public string Key => "purchases";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => Scheduler.Dispose());

        await Scheduler.ResumeAsync();

        await Scheduler.WaitAsync();
    }

    public async Task<BackgroundJobCounters> GetCountersAsync(MusicDbContext db, long userId,
        CancellationToken cancellationToken)
    {
        var countsByStatus = await db.PurchasedSongs
            .Where(p => p.UserId == userId)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        return new BackgroundJobCounters(
            countsByStatus.GetValueOrDefault(PurchasedSongStatus.Queued),
            countsByStatus.GetValueOrDefault(PurchasedSongStatus.Completed),
            countsByStatus.GetValueOrDefault(PurchasedSongStatus.Failed));
    }

    public Task<BackgroundJobFailurePage> GetFailuresAsync(MusicDbContext db, long userId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var failed = db.PurchasedSongs
            .Include(p => p.Source)
            .Where(p => p.UserId == userId && p.Status == PurchasedSongStatus.Failed)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .AsNoTracking();

        return BackgroundJobFailurePage.FromQueryAsync(failed, page, pageSize, p => new BackgroundJobFailure(
            p.Id.ToString(CultureInfo.InvariantCulture),
            $"{p.Title} ({p.SubTitle})",
            p.ErrorMessage,
            p.CreatedAt,
            [
                new("PurchaseId", p.Id.ToString(CultureInfo.InvariantCulture)),
                new("Title", p.Title),
                new("SubTitle", p.SubTitle),
                new("Source", p.Source.Name),
                new("SourceId", p.SourceId.ToString(CultureInfo.InvariantCulture)),
                new("ExternalId", p.ExternalId),
                new("Progress", p.Progress.ToString(CultureInfo.InvariantCulture)),
                new("CreatedAt", p.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
            ]), cancellationToken);
    }

    public class PurchasesScheduler(
        IServiceScopeFactory serviceScopeFactory,
        int maxParallelTasks)
        : BackgroundTaskScheduler<PurchasedSong>(maxParallelTasks, false)
    {
        protected override async Task<List<PurchasedSong>> PullNextTasksAsync(int count,
            CancellationToken cancellationToken)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();

            var queuedTasks = await context.PurchasedSongs
                .Where(x => x.Status == PurchasedSongStatus.Queued)
                .OrderBy(x => x.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return queuedTasks;
        }

        protected override async Task ExecuteTaskCoreAsync(PurchasedSong task, CancellationToken cancellationToken)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var executor = scope.ServiceProvider.GetRequiredService<PurchasesExecutor>();

            await executor.ExecuteAsync(task, cancellationToken);
        }

        protected override Task SetTaskPausedAsync(PurchasedSong task, CancellationToken cancellationToken) =>
            // No-op
            Task.CompletedTask;

        protected override async Task SetTaskRunningAsync(PurchasedSong task, CancellationToken cancellationToken)
        {
            task.Status = PurchasedSongStatus.Acquiring;

            await UpdateTaskStore(task, cancellationToken);
        }

        protected override async Task SetTaskFailedAsync(PurchasedSong task, string errorMessage,
            CancellationToken cancellationToken)
        {
            task.Status = PurchasedSongStatus.Failed;
            task.ErrorMessage = errorMessage;
            task.Progress = 100;

            await UpdateTaskStore(task, cancellationToken);
        }

        protected override async Task SetTaskFinishedAsync(PurchasedSong task, CancellationToken cancellationToken)
        {
            task.Status = PurchasedSongStatus.Completed;
            task.ErrorMessage = string.Empty;
            task.Progress = 100;

            await UpdateTaskStore(task, cancellationToken);
        }

        protected async Task UpdateTaskStore(PurchasedSong task, CancellationToken cancellationToken)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();

            var purchase = await context.PurchasedSongs.FindAsync([task.Id], cancellationToken);

            if (purchase is not null)
            {
                purchase.Status = task.Status;
                purchase.ErrorMessage = task.ErrorMessage;
                purchase.Progress = task.Progress;

                context.Update(purchase);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public class PurchasesExecutor(
        MusicDbContext db,
        IMusicService musicService,
        ISourcesService sourcesService,
        IFileSystem fileSystem,
        ISongFileReplaceService songFileReplace,
        MusicImportJob importJob)
    {
        public async Task ExecuteAsync(PurchasedSong purchase, CancellationToken cancellationToken)
        {
            var source = await sourcesService.GetSourceClientAsync(purchase.SourceId, cancellationToken);

            if (purchase.ReplacesSongFile)
            {
                await ReplaceSongFileAsync(source, purchase, cancellationToken);
                return;
            }

            var sourceSong = await source.GetSongAsync(purchase.ExternalId, cancellationToken);
            var metadata = SourcesConverter.ToSong(sourceSong);

            var stream = await source.PurchaseSongAsync(purchase.ExternalId, cancellationToken);

            var tempTarget = new FileTarget(fileSystem) { FilePath = Path.GetTempFileName() + ".mp3" };
            try
            {
                var naming = new NamingMetadata { Extension = ".mp3" };
                await tempTarget.Save(stream, metadata, naming, cancellationToken: cancellationToken);

                var now = DateTime.UtcNow;

                await musicService.ImportRepositorySongs(db, importJob, purchase.UserId, [
                        new SongImportMetadata(tempTarget.FilePath, now, now),
                    ], duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip,
                    cancellationToken: cancellationToken);

                importJob.ThrowIfAnyExceptions();

                var song = importJob.SongMapping.Values.FirstOrDefault();

                purchase.SongId = song?.Id;
                db.Update(purchase);
                await db.SaveChangesAsync(cancellationToken);

                if (song != null)
                {
                    var autoImportDevices = await db.Devices
                        .Where(d => d.OwnerId == purchase.UserId && d.ImportOnPurchase)
                        .ToListAsync(cancellationToken);

                    foreach (var device in autoImportDevices)
                    {
                        await musicService.AddSongsToDevice(db, device.Id, song, cancellationToken);
                    }

                    if (autoImportDevices.Count > 0)
                    {
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                File.Delete(tempTarget.FilePath);
            }
        }

        /// <summary>
        /// Purchases the song to use its audio only, in the place of the audio of the purchase's song. Nothing the
        /// source knows about the purchased song is kept.
        /// </summary>
        private async Task ReplaceSongFileAsync(ISource source, PurchasedSong purchase,
            CancellationToken cancellationToken)
        {
            if (purchase.SongId is not { } songId)
            {
                throw new InvalidOperationException("The song to replace the audio of no longer exists");
            }

            await using var stream = await source.PurchaseSongAsync(purchase.ExternalId, cancellationToken);

            var tempDirectory = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(),
                $"mymusic_purchase_{Guid.NewGuid()}");
            fileSystem.Directory.CreateDirectory(tempDirectory);

            try
            {
                var tempTarget = new FileTarget(fileSystem)
                {
                    FilePath = fileSystem.Path.Combine(tempDirectory, "purchase.mp3"),
                };
                await tempTarget.Save(stream, cancellationToken: cancellationToken);

                await songFileReplace.ReplaceAsync(purchase.UserId, songId, tempTarget.FilePath, cancellationToken);
            }
            finally
            {
                fileSystem.Directory.Delete(tempDirectory, true);
            }
        }
    }
}