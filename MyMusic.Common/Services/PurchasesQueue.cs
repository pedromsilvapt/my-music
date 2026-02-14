using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.Targets;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.Services;

public class PurchasesQueue(IServiceScopeFactory serviceScopeFactory)
    : BackgroundService
{
    public PurchasesScheduler Scheduler { get; } = new(serviceScopeFactory, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => Scheduler.Dispose());

        await Scheduler.ResumeAsync();

        await Scheduler.WaitAsync();
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

    internal class PurchasesExecutor(
        MusicDbContext db,
        IMusicService musicService,
        ISourcesService sourcesService,
        IFileSystem fileSystem,
        MusicImportJob importJob)
    {
        public async Task ExecuteAsync(PurchasedSong purchase, CancellationToken cancellationToken)
        {
            var source = await sourcesService.GetSourceClientAsync(purchase.SourceId, cancellationToken);

            var sourceSong = await source.GetSongAsync(purchase.ExternalId, cancellationToken);
            var metadata = SourcesConverter.ToSong(sourceSong);

            var stream = await source.PurchaseSongAsync(purchase.ExternalId, cancellationToken);

            var tempTarget = new FileTarget(fileSystem) { FilePath = Path.GetTempFileName() + ".mp3" };
            try
            {
                await tempTarget.Save(stream, metadata, cancellationToken);

                var now = DateTime.Now;

                await musicService.ImportRepositorySongs(db, importJob, purchase.UserId, [
                        new SongImportMetadata(tempTarget.FilePath, now, now),
                    ], duplicatesStrategy: DuplicateSongsHandlingStrategy.SkipIdentical,
                    cancellationToken: cancellationToken);

                importJob.ThrowIfAnyExceptions();

                var song = importJob.SongMapping.Values.FirstOrDefault();

                purchase.SongId = song?.Id;
                db.Update(purchase);
                await db.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                File.Delete(tempTarget.FilePath);
            }
        }
    }
}