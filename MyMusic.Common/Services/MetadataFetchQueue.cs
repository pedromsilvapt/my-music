using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Utilities;
using System.Text.Json;

namespace MyMusic.Common.Services;

/// <summary>
/// Background service for queueing and processing metadata fetch tasks.
/// Mirrors the PurchasesQueue pattern for consistency.
/// </summary>
public class MetadataFetchQueue(IServiceScopeFactory serviceScopeFactory)
    : BackgroundService
{
    public MetadataFetchScheduler Scheduler { get; } = new(serviceScopeFactory, 3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => Scheduler.Dispose());

        await Scheduler.ResumeAsync();

        await Scheduler.WaitAsync();
    }

    /// <summary>
    /// Scheduler that manages task lifecycle and orchestrates the queue.
    /// </summary>
    public class MetadataFetchScheduler(
        IServiceScopeFactory serviceScopeFactory,
        int maxParallelTasks)
        : BackgroundTaskScheduler<MetadataFetchTask>(maxParallelTasks, false)
    {
        // Error message patterns for categorization
        private const string TimeoutPattern = "timeout";
        private const string TimedOutPattern = "timed out";
        private const string ServiceUnavailablePattern = "service unavailable";
        private const string Error503Pattern = "503";
        private const string ServicePattern = "service";
        private const string NoMetadataFoundPattern = "no metadata found";
        private const string NotFoundPattern = "not found";
        private const string NoResultsPattern = "no results";
        private const string NetworkPattern = "network";
        private const string ConnectionPattern = "connection";
        private const string UnreachablePattern = "unreachable";
        protected override async Task<List<MetadataFetchTask>> PullNextTasksAsync(int count,
            CancellationToken cancellationToken)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();

            var queuedTasks = await context.MetadataFetchTasks
                .Where(x => x.Status == MetadataFetchStatus.Queued)
                .OrderBy(x => x.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return queuedTasks;
        }

        protected override async Task ExecuteTaskCoreAsync(MetadataFetchTask task, CancellationToken cancellationToken)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var executor = scope.ServiceProvider.GetRequiredService<MetadataFetchExecutor>();

            await executor.ExecuteAsync(task, cancellationToken);
        }

        protected override Task SetTaskPausedAsync(MetadataFetchTask task, CancellationToken cancellationToken) =>
            // No-op for this implementation
            Task.CompletedTask;

        protected override async Task SetTaskRunningAsync(MetadataFetchTask task, CancellationToken cancellationToken)
        {
            task.Status = MetadataFetchStatus.Processing;
            task.StartedAt = DateTime.UtcNow;

            await UpdateTaskStore(task, cancellationToken);
        }

        protected override async Task SetTaskFailedAsync(MetadataFetchTask task, string errorMessage,
            CancellationToken cancellationToken)
        {
            task.Status = MetadataFetchStatus.Failed;
            task.ErrorMessage = errorMessage;
            task.Progress = 100;
            task.CompletedAt = DateTime.UtcNow;
            task.FailureReason = CategorizeFailure(errorMessage);

            await UpdateTaskStore(task, cancellationToken);
        }

        /// <summary>
        /// Categorizes an error message into a failure reason.
        /// </summary>
        private static MetadataFetchFailureReason CategorizeFailure(string errorMessage)
        {
            var message = errorMessage.ToLowerInvariant();

            if (message.Contains(TimeoutPattern) || message.Contains(TimedOutPattern))
                return MetadataFetchFailureReason.Timeout;

            if (message.Contains(ServiceUnavailablePattern) || message.Contains(Error503Pattern) || message.Contains(ServicePattern))
                return MetadataFetchFailureReason.ServiceUnavailable;

            if (message.Contains(NoMetadataFoundPattern) || message.Contains(NotFoundPattern) || message.Contains(NoResultsPattern))
                return MetadataFetchFailureReason.NoMetadataFound;

            if (message.Contains(NetworkPattern) || message.Contains(ConnectionPattern) || message.Contains(UnreachablePattern))
                return MetadataFetchFailureReason.NetworkError;

            return MetadataFetchFailureReason.SystemError;
        }

        protected override async Task SetTaskFinishedAsync(MetadataFetchTask task, CancellationToken cancellationToken)
        {
            task.Status = MetadataFetchStatus.Completed;
            task.ErrorMessage = string.Empty;
            task.Progress = 100;
            task.CompletedAt = DateTime.UtcNow;

            await UpdateTaskStore(task, cancellationToken);
        }

        protected async Task UpdateTaskStore(MetadataFetchTask task, CancellationToken cancellationToken)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();

            var fetchTask = await context.MetadataFetchTasks.FindAsync([task.Id], cancellationToken);

            if (fetchTask is not null)
            {
                fetchTask.Status = task.Status;
                fetchTask.ErrorMessage = task.ErrorMessage;
                fetchTask.Progress = task.Progress;
                fetchTask.StartedAt = task.StartedAt;
                fetchTask.CompletedAt = task.CompletedAt;
                fetchTask.FailureReason = task.FailureReason;

                context.Update(fetchTask);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executor that performs the actual metadata fetching from external sources with progress tracking.
    /// </summary>
    internal class MetadataFetchExecutor(
        MusicDbContext db,
        ISourcesService sourcesService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<MetadataFetchExecutor> logger)
    {
        public async Task ExecuteAsync(MetadataFetchTask fetchTask, CancellationToken cancellationToken)
        {
            logger.LogInformation(
                "Fetching metadata for song {SongId} (Task: {TaskId})",
                fetchTask.SongId,
                fetchTask.Id);

            // Track progress
            int totalSteps = 4;
            int currentStep = 0;

            async Task UpdateProgress(int progress)
            {
                fetchTask.Progress = Math.Min(progress, 99); // Max 99 until complete
                await UpdateTaskProgressAsync(fetchTask, cancellationToken);
            }

            try
            {
                // Step 1: Get the song with related data (0-25%)
                currentStep++;
                await UpdateProgress((currentStep * 100) / totalSteps);

                var song = await db.Songs
                    .AsNoTracking()
                    .Include(s => s.Album)
                    .Include(s => s.Artists)
                    .ThenInclude(sa => sa.Artist)
                    .Include(s => s.Genres)
                    .ThenInclude(sg => sg.Genre)
                    .Include(s => s.Cover)
                    .FirstOrDefaultAsync(s => s.Id == fetchTask.SongId, cancellationToken);

                if (song is null)
                {
                    throw new InvalidOperationException($"Song not found with ID: {fetchTask.SongId}");
                }

                // Step 2: Query sources with retry logic (25-75%)
                currentStep++;
                await UpdateProgress((currentStep * 100) / totalSteps);

                var sources = await db.Sources
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                if (sources.Count == 0)
                {
                    throw new InvalidOperationException("No metadata sources configured");
                }

                // Build search query
                var artistNames = song.Artists.Select(a => a.Artist.Name).ToList();
                var searchQuery = $"{song.Title} {string.Join(" ", artistNames)}";

                logger.LogDebug("Searching for: {Query}", searchQuery);

                // Query all sources with retry logic
                var allResults = await QuerySourcesWithRetryAsync(sources, searchQuery, fetchTask, cancellationToken);

                if (allResults.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"No metadata found for song '{song.Title}' from any configured source");
                }

                // Step 3: Find best match and get full details (75-90%)
                currentStep++;
                await UpdateProgress((currentStep * 100) / totalSteps);

                var bestMatch = FindBestMatch(allResults, song);
                var bestSourceClient = await sourcesService.GetSourceClientAsync(bestMatch.Source.Id, cancellationToken);
                var fullDetails = await bestSourceClient.GetSongAsync(bestMatch.Song.Id, cancellationToken);

                logger.LogInformation(
                    "Found metadata from {SourceName} for song {SongTitle}",
                    bestMatch.Source.Name,
                    song.Title);

                // Step 4: Store raw source metadata (diff constructed at runtime) (90-100%)
                currentStep++;
                await UpdateProgress((currentStep * 100) / totalSteps);

                // Store the raw SourceSong response from the source
                // The metadata diff will be constructed at runtime using MetadataDiffBuilder
                // Use JsonDocument with Clone() to ensure JsonElement lifetime is properly managed
                using var sourceMetadataDoc = JsonDocument.Parse(JsonSerializer.Serialize(fullDetails));
                var sourceMetadataJson = sourceMetadataDoc.RootElement.Clone();

                var autoFetchedMetadata = new AutoFetchedMetadata
                {
                    SongId = song.Id,
                    SourceMetadata = sourceMetadataJson,
                    Status = AutoFetchStatus.Pending,
                    SourceId = bestMatch.Source.Id,
                    FetchedAt = DateTime.UtcNow
                };

                db.AutoFetchedMetadata.Add(autoFetchedMetadata);
                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "[AUDIT] Successfully stored metadata for song {SongId} (MetadataId: {MetadataId}) from source {SourceId}",
                    song.Id,
                    autoFetchedMetadata.Id,
                    bestMatch.Source.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to fetch metadata for song {SongId} (Task: {TaskId})",
                    fetchTask.SongId,
                    fetchTask.Id);

                // Create failed metadata record
                // Use JsonDocument with Clone() to ensure JsonElement lifetime is properly managed
                using var failedMetadataDoc = JsonDocument.Parse(JsonSerializer.Serialize(new { }));
                var failedMetadata = new AutoFetchedMetadata
                {
                    SongId = fetchTask.SongId,
                    SourceMetadata = failedMetadataDoc.RootElement.Clone(),
                    Status = AutoFetchStatus.Failed,
                    FetchedAt = DateTime.UtcNow,
                    ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 500)]
                };

                db.AutoFetchedMetadata.Add(failedMetadata);
                await db.SaveChangesAsync(cancellationToken);

                throw;
            }
        }

        private async Task<List<(Source Source, Sources.SourceSong Song)>> QuerySourcesWithRetryAsync(
            List<Source> sources,
            string searchQuery,
            MetadataFetchTask fetchTask,
            CancellationToken cancellationToken)
        {
            var allResults = new List<(Source Source, Sources.SourceSong Song)>();
            int maxRetries = 3;
            int sourcesCompleted = 0;

            foreach (var source in sources)
            {
                int retries = 0;
                bool success = false;

                while (retries < maxRetries && !success)
                {
                    try
                    {
                        var client = await sourcesService.GetSourceClientAsync(source.Id, cancellationToken);
                        var results = await client.SearchSongsAsync(searchQuery, cancellationToken);

                        if (results.Count > 0)
                        {
                            allResults.AddRange(results.Select(r => (source, r)));
                            logger.LogDebug(
                                "Source {SourceName} returned {Count} results",
                                source.Name,
                                results.Count);
                        }

                        success = true;
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        logger.LogWarning(
                            ex,
                            "Attempt {Retry}/{MaxRetries} failed for source {SourceId} ({SourceName})",
                            retries,
                            maxRetries,
                            source.Id,
                            source.Name);

                        if (retries >= maxRetries)
                        {
                            logger.LogError(
                                "Source {SourceName} failed after {MaxRetries} retries",
                                source.Name,
                                maxRetries);
                        }
                        else
                        {
                            // Exponential backoff: 1s, 2s, 4s
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retries - 1)), cancellationToken);
                        }
                    }
                }

                sourcesCompleted++;
                // Update progress within the source querying phase (25-75%)
                var sourceProgress = 25 + ((sourcesCompleted * 50) / sources.Count);
                fetchTask.Progress = Math.Min(sourceProgress, 74);
                await UpdateTaskProgressAsync(fetchTask, cancellationToken);
            }

            return allResults;
        }

        private async Task UpdateTaskProgressAsync(MetadataFetchTask task, CancellationToken cancellationToken)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<MusicDbContext>();

            var fetchTask = await context.MetadataFetchTasks.FindAsync([task.Id], cancellationToken);

            if (fetchTask is not null)
            {
                fetchTask.Progress = task.Progress;
                context.Update(fetchTask);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static (Source Source, Sources.SourceSong Song) FindBestMatch(
            List<(Source Source, Sources.SourceSong Song)> results,
            Song targetSong)
        {
            // Simple scoring algorithm:
            // 1. Exact title match (case-insensitive): +100 points
            // 2. Title contains target: +50 points
            // 3. Any artist matches: +30 points per artist
            // 4. Duration within 5 seconds: +20 points

            var scoredResults = results.Select(r =>
            {
                var score = 0;
                var targetArtists = targetSong.Artists.Select(a => a.Artist.Name.ToLower()).ToHashSet();

                // Title scoring
                var resultTitle = r.Song.Title.ToLower();
                var targetTitle = targetSong.Title.ToLower();

                if (resultTitle == targetTitle)
                {
                    score += 100;
                }
                else if (resultTitle.Contains(targetTitle) || targetTitle.Contains(resultTitle))
                {
                    score += 50;
                }

                // Artist scoring
                foreach (var artist in r.Song.Artists)
                {
                    if (targetArtists.Contains(artist.Name.ToLower()))
                    {
                        score += 30;
                    }
                }

                // Duration scoring (if available)
                if (targetSong.Duration > TimeSpan.Zero && r.Song.Duration > TimeSpan.Zero)
                {
                    var durationDiff = Math.Abs((r.Song.Duration - targetSong.Duration).TotalSeconds);
                    if (durationDiff < 5)
                    {
                        score += 20;
                    }
                }

                return (r.Source, r.Song, Score: score);
            });

            return scoredResults
                .OrderByDescending(r => r.Score)
                .Select(r => (r.Source, r.Song))
                .First();
        }
    }
}
